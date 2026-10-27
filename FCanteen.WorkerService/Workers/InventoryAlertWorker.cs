using FCanteen.Data;
using FCanteen.Data.Dtos;
using FCanteen.Data.Entities;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace FCanteen.WorkerService.Workers
{
    /// <summary>
    /// Worker quét cảnh báo tồn kho (YC2, YC4):
    /// - Chạy chu kỳ định kỳ bằng PeriodicTimer (mặc định 60s)
    /// - Gọi GET /api/inventory/low-stock qua IHttpClientFactory
    /// - Cơ chế thử lại 3 lần với Exponential Backoff (2s, 4s, 8s) khi Web API lỗi (YC4)
    /// - Sử dụng IServiceScopeFactory tạo Scoped DbContext trong Singleton Worker (Tránh Captive Dependency)
    /// - Chống ghi trùng cảnh báo chưa xử lý
    /// - Đẩy cảnh báo thời gian thực tới KitchenServer qua SignalR Hub
    /// </summary>
    public class InventoryAlertWorker : BackgroundService
    {
        private readonly ILogger<InventoryAlertWorker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private HubConnection? _hubConnection;

        public InventoryAlertWorker(
            ILogger<InventoryAlertWorker> logger,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // Khởi tạo kết nối SignalR Hub tới KitchenServer
            var hubUrl = _configuration["SignalRSettings:HubUrl"] ?? "http://localhost:5297/orderHub";
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            _hubConnection.Closed += async (error) =>
            {
                _logger.LogWarning($"[SignalR] Mất kết nối tới KitchenServer Hub: {error?.Message}. Đang chờ tự động kết nối lại...");
                await Task.CompletedTask;
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation($"[SignalR] Đã kết nối lại thành công tới KitchenServer Hub (ID: {connectionId})");
                await Task.CompletedTask;
            };

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int intervalSeconds = _configuration.GetValue<int>("WorkerSettings:InventoryCheckIntervalSeconds", 60);
            _logger.LogInformation($"[InventoryAlertWorker] Worker khởi động thành công. Chu kỳ quét: {intervalSeconds} giây (sử dụng PeriodicTimer).");

            // Đảm bảo SignalR kết nối nếu KitchenServer đã bật
            await EnsureSignalRConnectedAsync(stoppingToken);

            // Bắt buộc dùng PeriodicTimer theo yêu cầu đề bài (không dùng Task.Delay trong vòng while)
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

            // Chạy ngay lần đầu tiên khi vừa khởi động
            await RunCycleWithFaultToleranceAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleWithFaultToleranceAsync(stoppingToken);
            }

            _logger.LogInformation("[InventoryAlertWorker] Worker đang dừng.");
        }

        /// <summary>
        /// YC4: Bọc toàn bộ chu kỳ quét trong cơ chế chịu lỗi để Web API sập không bao giờ làm dừng Worker
        /// </summary>
        private async Task RunCycleWithFaultToleranceAsync(CancellationToken stoppingToken)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation($"\n=======================================================");
            _logger.LogInformation($"[InventoryAlertWorker] >>> BẮT ĐẦU CHU KỲ QUÉT TỒN KHO lúc {DateTime.Now:HH:mm:ss dd/MM/yyyy}");

            try
            {
                // Bước 1: Gọi Web API với cơ chế Retry 3 lần kèm Exponential Backoff (YC4)
                var lowStockItems = await FetchLowStockWithRetryAsync(stoppingToken);

                if (lowStockItems == null)
                {
                    _logger.LogWarning("[InventoryAlertWorker] [YC4 CHỊU LỖI] Không thể lấy dữ liệu từ Web API sau 3 lần thử. Bỏ qua chu kỳ này. Worker vẫn an toàn chờ chu kỳ kế tiếp.");
                    return;
                }

                _logger.LogInformation($"[InventoryAlertWorker] Web API phản hồi: phát hiện {lowStockItems.Count} nguyên liệu đang dưới hoặc bằng ngưỡng cảnh báo.");

                // Bước 2: Xử lý lưu StockAlert vào DB (dùng IServiceScopeFactory để giải quyết Captive Dependency)
                int newAlertCount = 0;
                int duplicateSkippedCount = 0;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<FCanteenContext>();

                    foreach (var item in lowStockItems)
                    {
                        // Kiểm tra xem cảnh báo cho nguyên liệu này đã tồn tại và chưa được xử lý (IsResolved == false) hay chưa
                        bool alertExists = await context.StockAlerts
                            .AnyAsync(a => a.IngredientId == item.Id && !a.IsResolved, stoppingToken);

                        if (alertExists)
                        {
                            duplicateSkippedCount++;
                            _logger.LogInformation($"[InventoryAlertWorker] [BỎ QUA TRÙNG LẶP] Nguyên liệu '{item.Name}' (ID: {item.Id}) đang thiếu nhưng đã có cảnh báo chưa xử lý trong DB.");
                            continue;
                        }

                        // Ghi nhận StockAlert mới
                        var alert = new StockAlert
                        {
                            IngredientId = item.Id,
                            IngredientName = item.Name,
                            CurrentStock = item.Stock,
                            WarningThreshold = item.WarningThreshold,
                            AlertTime = DateTime.Now,
                            IsResolved = false,
                            Note = $"Tự động phát hiện bởi InventoryAlertWorker. Tồn kho: {item.Stock} {item.Unit} <= Ngưỡng: {item.WarningThreshold} {item.Unit}"
                        };

                        context.StockAlerts.Add(alert);
                        await context.SaveChangesAsync(stoppingToken);
                        newAlertCount++;

                        _logger.LogWarning($"[InventoryAlertWorker] [CẢNH BÁO MỚI] Đã tạo StockAlert cho '{item.Name}': Tồn kho={item.Stock} {item.Unit}, Ngưỡng={item.WarningThreshold} {item.Unit}");

                        // Bước 3: Đẩy cảnh báo tới giao diện quản lý qua SignalR (YC2)
                        var alertDto = new StockAlertDto
                        {
                            Id = alert.Id,
                            IngredientId = item.Id,
                            IngredientName = item.Name,
                            CurrentStock = item.Stock,
                            WarningThreshold = item.WarningThreshold,
                            Unit = item.Unit,
                            AlertTime = alert.AlertTime,
                            Note = alert.Note
                        };

                        await PushSignalRAlertAsync(alertDto, stoppingToken);
                    }
                }

                _logger.LogInformation($"[InventoryAlertWorker] Tổng kết chu kỳ: {lowStockItems.Count} nguyên liệu quét, {newAlertCount} cảnh báo mới, {duplicateSkippedCount} cảnh báo trùng được bỏ qua.");
            }
            catch (Exception ex)
            {
                // Bắt mọi ngoại lệ bất ngờ để đảm bảo không văng khỏi ExecuteAsync
                _logger.LogError(ex, $"[InventoryAlertWorker] Lỗi không mong muốn trong chu kỳ: {ex.Message}. Worker tiếp tục chạy bình thường.");
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation($"[InventoryAlertWorker] <<< KẾT THÚC CHU KỲ QUÉT TỒN KHO. Thời gian thực thi: {stopwatch.ElapsedMilliseconds} ms.");
                _logger.LogInformation($"=======================================================\n");
            }
        }

        /// <summary>
        /// YC4: Gọi GET /api/inventory/low-stock với cơ chế thử lại tối đa 3 lần với độ trễ tăng dần (Exponential Backoff: 2s, 4s, 8s)
        /// </summary>
        private async Task<List<InventoryItemDto>?> FetchLowStockWithRetryAsync(CancellationToken stoppingToken)
        {
            int maxRetries = _configuration.GetValue<int>("WorkerSettings:MaxRetryCount", 3);
            var client = _httpClientFactory.CreateClient("FCanteenApi");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await client.GetAsync("/api/inventory/low-stock", stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(stoppingToken);
                        return JsonSerializer.Deserialize<List<InventoryItemDto>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }

                    _logger.LogWarning($"[InventoryAlertWorker] Gọi Web API trả về mã lỗi: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException)
                {
                    int delaySeconds = (int)Math.Pow(2, attempt); // 2s, 4s, 8s (Exponential Backoff)
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning($"[InventoryAlertWorker] [YC4 RETRY {attempt}/{maxRetries}] Lỗi kết nối Web API ({ex.Message}). Đang thử lại sau {delaySeconds} giây...");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                    }
                    else
                    {
                        _logger.LogError($"[InventoryAlertWorker] [YC4 RETRY THẤT BẠI] Đã thử lại đủ {maxRetries} lần nhưng không thể kết nối tới Web API ({ex.Message}).");
                    }
                }
            }

            return null; // Trả về null khi cả 3 lần thử đều thất bại
        }

        /// <summary>
        /// Kết nối và bắn sự kiện cảnh báo qua SignalR
        /// </summary>
        private async Task PushSignalRAlertAsync(StockAlertDto alertDto, CancellationToken stoppingToken)
        {
            try
            {
                await EnsureSignalRConnectedAsync(stoppingToken);
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    await _hubConnection.InvokeAsync("SendLowStockAlert", alertDto, stoppingToken);
                    _logger.LogInformation($"[SignalR] Đã phát sóng cảnh báo tồn kho '{alertDto.IngredientName}' tới toàn bộ client trên Hub.");
                }
                else
                {
                    _logger.LogWarning("[SignalR] Không thể gửi cảnh báo vì SignalR Hub chưa sẵn sàng. Cảnh báo vẫn đã được lưu trong DB.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[SignalR] Lỗi khi gửi sự kiện SignalR: {ex.Message}. Không làm ảnh hưởng luồng chính của Worker.");
            }
        }

        private async Task EnsureSignalRConnectedAsync(CancellationToken stoppingToken)
        {
            if (_hubConnection == null) return;

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await _hubConnection.StartAsync(stoppingToken);
                    _logger.LogInformation("[SignalR] Đã kết nối thành công tới KitchenServer SignalR Hub (/orderHub).");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[SignalR] Chưa thể kết nối tới KitchenServer Hub ({ex.Message}). Sẽ tự động kết nối lại ở các chu kỳ sau.");
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_hubConnection != null)
            {
                try
                {
                    await _hubConnection.DisposeAsync();
                }
                catch { }
            }
            await base.StopAsync(cancellationToken);
        }
    }
}
