using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FCanteen.WorkerService.Workers
{
    /// <summary>
    /// Worker chốt báo cáo ngày (YC3):
    /// - Chạy vào một giờ cố định trong ngày (hoặc cấu hình 2 phút ở chế độ Development để tiện chấm bài)
    /// - Tổng hợp doanh thu, số đơn, món bán chạy, tỉ lệ đơn huỷ của ngày hôm trước
    /// - Ghi vào bảng DailyReport
    /// - Bỏ qua nếu báo cáo của ngày đó đã tồn tại (chống sinh dữ liệu trùng khi chạy lại nhiều lần)
    /// </summary>
    public class DailyReportWorker : BackgroundService
    {
        private readonly ILogger<DailyReportWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public DailyReportWorker(
            ILogger<DailyReportWorker> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _environment = environment;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Kiểm tra cấu hình chế độ chạy
            var runMode = _configuration["WorkerSettings:DailyReportRunMode"] ?? "Interval";
            int intervalMinutes = _configuration.GetValue<int>("WorkerSettings:DailyReportIntervalMinutes", 2);

            if (_environment.IsDevelopment() || runMode.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation($"[DailyReportWorker] Đang chạy ở chế độ Development/Interval: Chu kỳ chốt báo cáo mỗi {intervalMinutes} phút (sử dụng PeriodicTimer).");

                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

                // Chạy ngay một lần khi khởi động
                await ExecuteDailySettlementAsync(stoppingToken);

                while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ExecuteDailySettlementAsync(stoppingToken);
                }
            }
            else
            {
                // Chế độ Production: Chạy vào giờ cố định mỗi ngày (mặc định 00:05)
                var scheduledTimeStr = _configuration["WorkerSettings:DailyReportScheduledTime"] ?? "00:05";
                if (!TimeSpan.TryParse(scheduledTimeStr, out var scheduledTime))
                {
                    scheduledTime = new TimeSpan(0, 5, 0);
                }

                _logger.LogInformation($"[DailyReportWorker] Đang chạy ở chế độ Production: Lên lịch chốt báo cáo lúc {scheduledTime} hàng ngày.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    var now = DateTime.Now;
                    var nextRun = DateTime.Today.Add(scheduledTime);
                    if (now >= nextRun)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    var delay = nextRun - now;
                    _logger.LogInformation($"[DailyReportWorker] Lần chốt báo cáo tiếp theo lúc {nextRun:HH:mm:ss dd/MM/yyyy} (còn lại: {delay.TotalHours:F1} giờ).");

                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                        await ExecuteDailySettlementAsync(stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ExecuteDailySettlementAsync(CancellationToken stoppingToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var targetDate = DateTime.Today.AddDays(-1); // Ngày hôm trước theo yêu cầu đề bài
            var nextDate = targetDate.AddDays(1);

            _logger.LogInformation($"\n-------------------------------------------------------");
            _logger.LogInformation($"[DailyReportWorker] >>> BẮT ĐẦU CHỐT BÁO CÁO NGÀY {targetDate:yyyy-MM-dd} lúc {DateTime.Now:HH:mm:ss}");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<FCanteenContext>();

                // YC3: Bỏ qua nếu báo cáo của ngày đó đã tồn tại để không sinh dữ liệu trùng
                bool reportExists = await context.DailyReports
                    .AnyAsync(r => r.ReportDate.Date == targetDate.Date, stoppingToken);

                if (reportExists)
                {
                    _logger.LogInformation($"[DailyReportWorker] [IDEMPOTENT - BỎ QUA] Báo cáo ngày {targetDate:yyyy-MM-dd} đã tồn tại trong CSDL. Không sinh dữ liệu trùng.");
                    return;
                }

                // Truy vấn danh sách đơn hàng của ngày hôm trước
                var orders = await context.OrderTickets
                    .Where(o => o.CreatedAt >= targetDate && o.CreatedAt < nextDate)
                    .Include(o => o.TicketLines)
                        .ThenInclude(tl => tl.MenuItem)
                    .AsNoTracking()
                    .ToListAsync(stoppingToken);

                int totalOrders = orders.Count;
                int cancelledOrdersCount = orders.Count(o => o.Status == (int)OrderStatus.Cancelled);
                decimal cancellationRate = totalOrders > 0
                    ? Math.Round(((decimal)cancelledOrdersCount / totalOrders) * 100, 2)
                    : 0;

                var validOrders = orders.Where(o => o.Status != (int)OrderStatus.Cancelled).ToList();
                decimal totalRevenue = validOrders.Sum(o => o.TotalAmount);

                // Tìm món bán chạy nhất
                var topSelling = validOrders
                    .SelectMany(o => o.TicketLines)
                    .GroupBy(tl => new { tl.MenuItemId, ItemName = tl.MenuItem != null ? tl.MenuItem.Name : tl.MenuItemId })
                    .Select(g => new
                    {
                        Name = g.Key.ItemName,
                        TotalQuantity = g.Sum(tl => tl.Quantity)
                    })
                    .OrderByDescending(x => x.TotalQuantity)
                    .FirstOrDefault();

                var dailyReport = new DailyReport
                {
                    ReportDate = targetDate,
                    TotalRevenue = totalRevenue,
                    TotalOrders = totalOrders,
                    CancelledOrdersCount = cancelledOrdersCount,
                    CancellationRate = cancellationRate,
                    TopSellingItem = topSelling != null ? topSelling.Name : "Không có",
                    TopSellingQuantity = topSelling != null ? topSelling.TotalQuantity : 0,
                    CreatedAt = DateTime.Now
                };

                context.DailyReports.Add(dailyReport);
                await context.SaveChangesAsync(stoppingToken);

                _logger.LogInformation($"[DailyReportWorker] [THÀNH CÔNG] Đã chốt và lưu báo cáo ngày {targetDate:yyyy-MM-dd}:");
                _logger.LogInformation($"  - Doanh thu: {dailyReport.TotalRevenue:N0} đ");
                _logger.LogInformation($"  - Tổng số đơn: {dailyReport.TotalOrders}");
                _logger.LogInformation($"  - Số đơn huỷ: {dailyReport.CancelledOrdersCount} (Tỉ lệ: {dailyReport.CancellationRate}%)");
                _logger.LogInformation($"  - Món bán chạy nhất: {dailyReport.TopSellingItem} ({dailyReport.TopSellingQuantity} phần)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[DailyReportWorker] Lỗi khi chốt báo cáo ngày: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation($"[DailyReportWorker] <<< KẾT THÚC CHỐT BÁO CÁO NGÀY. Thời gian: {stopwatch.ElapsedMilliseconds} ms.");
                _logger.LogInformation($"-------------------------------------------------------\n");
            }
        }
    }
}
