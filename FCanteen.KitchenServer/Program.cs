using FCanteen.Data;
using FCanteen.Data.Entities;
using FCanteen.KitchenServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            await SyncPricesAsync();

            // YC1: Mồi 50k phiếu nếu chưa đủ
            using var context = GetDbContext();
            await FCanteen.Data.DatabaseSeeder.SeedLargeDataAsync(context);

            // Chạy lần lượt các bài test YC2 → YC5
            RunYC2_Benchmark();
            RunYC3_PLINQ();
            await RunYC4_EFCoreAsync();
            RunYC5_RaceCondition();

            // Phần TCP Server của Lab 01 vẫn chạy bình thường bên dưới
            _ = HandleAdminCommandsAsync();

            TcpListener server = new TcpListener(IPAddress.Any, 9500);
            server.Start();
            Console.WriteLine("[BẾP] Máy chủ đang chạy tại cổng 9500...");

            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                Console.WriteLine($"[BẾP] Kết nối từ: {((IPEndPoint)client.Client.RemoteEndPoint!).Address}");
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[LỖI SERVER] {ex.ToString()}"); }
    }

    // ===================== PHẦN TCP CỦA LAB 01 =====================

    static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer);
                string jsonRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                using var context = GetDbContext();

                context.DeviceLogs.Add(new DeviceLog { Protocol = "TCP", SourceAddress = clientIp, Content = $"Nhận JSON: {jsonRequest}" });
                await context.SaveChangesAsync();

                var request = JsonSerializer.Deserialize<OrderRequest>(jsonRequest);
                if (request == null) return;

                using var transaction = await context.Database.BeginTransactionAsync();

                string newTicketId = "T" + DateTime.Now.ToString("yyyyMMddHHmmssff");
                decimal recalculatedTotal = 0;
                var ticketLines = new List<TicketLine>();

                // Tính lại giá từ DB để tránh client gửi giá sai
                foreach (var line in request.Lines)
                {
                    var menuItem = await context.MenuItems.FindAsync(line.MenuItemId);
                    if (menuItem != null)
                    {
                        recalculatedTotal += menuItem.Price * line.Quantity;
                        ticketLines.Add(new TicketLine
                        {
                            MenuItemId = line.MenuItemId,
                            Quantity = line.Quantity,
                            UnitPrice = menuItem.Price,
                            Note = line.Note
                        });
                    }
                }

                var newTicket = new OrderTicket
                {
                    TicketId = newTicketId,
                    PosName = request.PosName,
                    TotalAmount = recalculatedTotal,
                    Status = "Pending",
                    TicketLines = ticketLines
                };

                context.OrderTickets.Add(newTicket);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"[CHỜ CHẾ BIẾN] Phiếu {newTicketId} từ {request.PosName} | Tổng: {recalculatedTotal:N0}đ");

                string jsonResponse = JsonSerializer.Serialize(new OrderResponse
                { TicketId = newTicketId, FinalTotalAmount = recalculatedTotal, Message = "Đã nhận phiếu thành công." });

                byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                await stream.WriteAsync(responseBytes);

                context.DeviceLogs.Add(new DeviceLog { Protocol = "TCP", SourceAddress = clientIp, Content = $"Phản hồi JSON: {jsonResponse}" });
                await context.SaveChangesAsync();
            }
            catch (Exception ex) { Console.WriteLine($"[LỖI CLIENT TASK] {ex.Message}"); }
        }
    }

    static FCanteenContext GetDbContext()
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
        return new FCanteenContext(new DbContextOptionsBuilder<FCanteenContext>().UseSqlServer(config.GetConnectionString("DefaultConnection")).Options);
    }

    static async Task SyncPricesAsync()
    {
        Console.WriteLine("\n--- YC4: ĐỒNG BỘ GIÁ TỪ SERVER TRUNG TÂM ---");
        Uri uri = new Uri("https://raw.githubusercontent.com/minhtan123/dummy/main/prices.json");

        Console.WriteLine($"[DNS] Scheme: {uri.Scheme}, Host: {uri.Host}, Port: {uri.Port}");
        var ips = await Dns.GetHostAddressesAsync(uri.Host);
        Console.WriteLine($"[DNS] IP Addresses: {string.Join(", ", ips.Select(ip => ip.ToString()))}");

        using var httpClient = new HttpClient();
        using var context = GetDbContext();
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync(uri);
            sw.Stop();

            context.DeviceLogs.Add(new DeviceLog
            {
                Protocol = "HTTP",
                SourceAddress = uri.Host,
                Content = $"Đồng bộ giá. Status: {response.StatusCode}. Thời gian: {sw.ElapsedMilliseconds}ms"
            });
            await context.SaveChangesAsync();

            if (response.IsSuccessStatusCode)
            {
                var priceUpdates = await response.Content.ReadFromJsonAsync<List<PriceUpdateDto>>();
                int updateCount = 0;
                foreach (var update in priceUpdates ?? new())
                {
                    var item = await context.MenuItems.FindAsync(update.Id);
                    if (item != null && item.Price != update.Price)
                    {
                        item.Price = update.Price;
                        updateCount++;
                    }
                }
                if (updateCount > 0) await context.SaveChangesAsync();
                Console.WriteLine($"[HTTP] Đã cập nhật giá cho {updateCount} món (Phản hồi: {sw.ElapsedMilliseconds}ms)\n");
            }
        }
        catch (Exception ex) { Console.WriteLine($"[LỖI HTTP]: {ex.Message}"); }
    }

    static async Task HandleAdminCommandsAsync()
    {
        using var udpServer = new UdpClient { EnableBroadcast = true };
        var endPoint = new IPEndPoint(IPAddress.Broadcast, 9501);

        while (true)
        {
            string? input = Console.ReadLine()?.Trim();
            if (input != null && input.StartsWith("HET "))
            {
                string itemId = input.Substring(4);
                using var context = GetDbContext();
                var item = await context.MenuItems.FindAsync(itemId);

                if (item != null)
                {
                    item.IsAvailable = false;
                    await context.SaveChangesAsync();

                    byte[] bytes = Encoding.UTF8.GetBytes(itemId);
                    await udpServer.SendAsync(bytes, bytes.Length, endPoint);
                    Console.WriteLine($"[UDP BROADCAST] Đã phát thông báo hết món: {itemId}");
                }
                else Console.WriteLine($"[LỖI] Không tìm thấy món {itemId}");
            }
        }
    }

    // ===================== YC2: SO SÁNH TUẦN TỰ VS SONG SONG =====================

    static void RunYC2_Benchmark()
    {
        Console.WriteLine("\n=== BẮT ĐẦU TEST HIỆU NĂNG (YC2) ===");
        using var context = GetDbContext();

        // Nạp hết data lên RAM trước → chỉ đo tốc độ CPU, không đo I/O Database
        Console.WriteLine("Đang nạp dữ liệu từ DB lên RAM...");
        var menuItems = context.MenuItems.ToList();
        var allLines = context.TicketLines.ToList();
        Console.WriteLine($"Đã nạp {menuItems.Count} món ăn và {allLines.Count} chi tiết vé.");

        var tester = new PerformanceTestService();

        long timeSeq = tester.RunSequential(menuItems, allLines);
        Console.WriteLine($"[1] Tuần tự (foreach): {timeSeq} ms");

        long timePForEach = tester.RunParallelForEach(menuItems, allLines);
        Console.WriteLine($"[2] Parallel.ForEach: {timePForEach} ms (Nhanh gấp {((double)timeSeq / timePForEach):F2} lần)");

        int logicalCores = Environment.ProcessorCount;
        Console.WriteLine($"=> Máy của bạn có {logicalCores} Logical Cores.");

        long timePFor2 = tester.RunParallelFor(menuItems, allLines, 2);
        Console.WriteLine($"[3] Parallel.For (Max 2 Cores): {timePFor2} ms");

        long timePFor4 = tester.RunParallelFor(menuItems, allLines, 4);
        Console.WriteLine($"[4] Parallel.For (Max 4 Cores): {timePFor4} ms");

        long timePForMax = tester.RunParallelFor(menuItems, allLines, logicalCores);
        Console.WriteLine($"[5] Parallel.For (Max {logicalCores} Cores): {timePForMax} ms (Nhanh gấp {((double)timeSeq / timePForMax):F2} lần)");

        // Ghi kết quả xuống DailySettlement
        var settlement = new DailySettlement
        {
            Date = DateTime.Now,
            BranchCode = "B01",
            TotalTickets = context.OrderTickets.Count(),
            TotalRevenue = context.OrderTickets.Sum(t => t.TotalAmount),
            CalculationTimeMs = timePForMax
        };
        context.DailySettlements.Add(settlement);
        context.SaveChanges();
        Console.WriteLine("Đã lưu kết quả tốc độ vào bảng DailySettlement!");
    }

    // ===================== YC3: BÁO CÁO BẰNG PLINQ =====================

    static void RunYC3_PLINQ()
    {
        Console.WriteLine("\n=== BẮT ĐẦU BÁO CÁO PLINQ (YC3) ===");
        using var context = GetDbContext();

        var tickets = context.OrderTickets.Include(t => t.TicketLines).ToList();
        var menuItems = context.MenuItems.OrderBy(m => m.Id).ToList();

        decimal totalRevenueAll = tickets.Sum(t => t.TotalAmount);

        // Báo cáo 1: Top 10 món doanh thu cao nhất
        var sw = Stopwatch.StartNew();
        var top10Seq = tickets.SelectMany(t => t.TicketLines)
                              .GroupBy(l => l.MenuItemId)
                              .Select(g => new { Id = g.Key, Rev = g.Sum(l => l.Quantity * l.UnitPrice) })
                              .OrderByDescending(x => x.Rev).Take(10).ToList();
        long t1Seq = sw.ElapsedMilliseconds;

        sw.Restart();
        var top10Par = tickets.AsParallel().SelectMany(t => t.TicketLines)
                              .GroupBy(l => l.MenuItemId)
                              .Select(g => new { Id = g.Key, Rev = g.Sum(l => l.Quantity * l.UnitPrice) })
                              .OrderByDescending(x => x.Rev).Take(10).ToList();
        long t1Par = sw.ElapsedMilliseconds;
        Console.WriteLine($"[1] Top 10 món doanh thu cao: Tuần tự {t1Seq}ms | PLINQ {t1Par}ms");

        // Báo cáo 2: Doanh thu theo từng khung giờ
        sw.Restart();
        var revByHourSeq = tickets.GroupBy(t => t.CreatedAt.Hour)
                                  .Select(g => new { Hour = g.Key, Rev = g.Sum(t => t.TotalAmount) }).ToList();
        long t2Seq = sw.ElapsedMilliseconds;

        sw.Restart();
        var revByHourPar = tickets.AsParallel().GroupBy(t => t.CreatedAt.Hour)
                                  .Select(g => new { Hour = g.Key, Rev = g.Sum(t => t.TotalAmount) }).ToList();
        long t2Par = sw.ElapsedMilliseconds;
        Console.WriteLine($"[2] Doanh thu theo giờ: Tuần tự {t2Seq}ms | PLINQ {t2Par}ms");

        // Báo cáo 3: Cơ sở doanh thu cao nhất mỗi tháng
        sw.Restart();
        var bestBranchSeq = tickets.GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month, t.BranchCode })
                                   .Select(g => new { g.Key.Year, g.Key.Month, g.Key.BranchCode, Rev = g.Sum(t => t.TotalAmount) })
                                   .GroupBy(x => new { x.Year, x.Month })
                                   .Select(g => g.OrderByDescending(x => x.Rev).First()).ToList();
        long t3Seq = sw.ElapsedMilliseconds;

        sw.Restart();
        var bestBranchPar = tickets.AsParallel().GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month, t.BranchCode })
                                   .Select(g => new { g.Key.Year, g.Key.Month, g.Key.BranchCode, Rev = g.Sum(t => t.TotalAmount) })
                                   .GroupBy(x => new { x.Year, x.Month })
                                   .Select(g => g.OrderByDescending(x => x.Rev).First()).ToList();
        long t3Par = sw.ElapsedMilliseconds;
        Console.WriteLine($"[3] Cơ sở Top 1 mỗi tháng: Tuần tự {t3Seq}ms | PLINQ {t3Par}ms");

        // Báo cáo 4: Món < 1% tổng doanh thu → đề xuất loại bỏ
        // Dùng AsOrdered() vì cần giữ đúng thứ tự menu gốc (M01, M02...)
        // 3 báo cáo trên không cần vì đã OrderBy/GroupBy lại rồi
        sw.Restart();
        var underperfSeq = menuItems.Where(m =>
            tickets.SelectMany(t => t.TicketLines).Where(l => l.MenuItemId == m.Id).Sum(l => l.Quantity * l.UnitPrice) < (totalRevenueAll * 0.01m)
        ).ToList();
        long t4Seq = sw.ElapsedMilliseconds;

        sw.Restart();
        var underperfPar = menuItems.AsParallel().AsOrdered().Where(m =>
            tickets.AsParallel().SelectMany(t => t.TicketLines).Where(l => l.MenuItemId == m.Id).Sum(l => l.Quantity * l.UnitPrice) < (totalRevenueAll * 0.01m)
        ).ToList();
        long t4Par = sw.ElapsedMilliseconds;
        Console.WriteLine($"[4] Món < 1% doanh thu (AsOrdered): Tuần tự {t4Seq}ms | PLINQ {t4Par}ms");

        Console.WriteLine($"> Đề xuất loại bỏ: {string.Join(", ", underperfPar.Select(m => m.Name))}");
    }

    // ===================== YC4: BẤT ĐỒNG BỘ VỚI EF CORE =====================

    static async Task RunYC4_EFCoreAsync()
    {
        Console.WriteLine("\n=== BẮT ĐẦU BẤT ĐỒNG BỘ EF CORE (YC4) ===");

        // IAsyncEnumerable: duyệt từng dòng từ DB mà không nạp hết vào RAM
        Console.WriteLine(">> 1. Duyệt phiếu giá trị lớn (IAsyncEnumerable):");
        using var context = GetDbContext();
        var largeTickets = context.OrderTickets.Where(t => t.TotalAmount > 100000).AsAsyncEnumerable();

        int count = 0;
        await foreach (var ticket in largeTickets)
        {
            count++;
            if (count <= 3) Console.WriteLine($"   - Phiếu: {ticket.TicketId} | Tiền: {ticket.TotalAmount:N0}đ");
        }
        Console.WriteLine($"   (Đã duyệt tổng cộng {count} phiếu lớn)");

        // CancellationToken: tự hủy nếu quá 30 giây
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken token = cts.Token;

        try
        {
            // Chạy nối tiếp: await từng query một
            Console.WriteLine("\n>> 2. Đang đo tốc độ Await Nối Tiếp...");
            var sw = Stopwatch.StartNew();

            using var ctx1 = GetDbContext();
            await ctx1.OrderTickets.SumAsync(t => t.TotalAmount, token);
            await ctx1.TicketLines.AverageAsync(l => l.UnitPrice, token);
            await ctx1.MenuItems.CountAsync(token);
            await ctx1.DeviceLogs.CountAsync(token);

            sw.Stop();
            long timeSequential = sw.ElapsedMilliseconds;
            Console.WriteLine($"Thời gian chạy nối tiếp: {timeSequential} ms");

            // Chạy đồng thời: mỗi query dùng 1 DbContext riêng (vì DbContext không thread-safe)
            Console.WriteLine("\n>> 3. Đang đo tốc độ Task.WhenAll (Đồng thời)...");
            sw.Restart();

            using var ctxA = GetDbContext();
            using var ctxB = GetDbContext();
            using var ctxC = GetDbContext();
            using var ctxD = GetDbContext();

            var taskA = ctxA.OrderTickets.SumAsync(t => t.TotalAmount, token);
            var taskB = ctxB.TicketLines.AverageAsync(l => l.UnitPrice, token);
            var taskC = ctxC.MenuItems.CountAsync(token);
            var taskD = ctxD.DeviceLogs.CountAsync(token);

            await Task.WhenAll(taskA, taskB, taskC, taskD);

            sw.Stop();
            long timeConcurrent = sw.ElapsedMilliseconds;
            Console.WriteLine($"Thời gian chạy Task.WhenAll: {timeConcurrent} ms (Nhanh gấp {(double)timeSequential / timeConcurrent:F2} lần)");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[CẢNH BÁO] Quá 30 giây → CancellationToken đã hủy!");
        }
    }

    // ===================== YC5: RACE CONDITION =====================

    static void RunYC5_RaceCondition()
    {
        Console.WriteLine("\n=== BẮT ĐẦU YC5: RACE CONDITION TRỪ TỒN KHO ===");

        // Bản lỗi: không lock → nhiều luồng đọc/ghi Stock cùng lúc → sai số
        var gao = new Ingredient { Name = "Gạo", Stock = 10000 };
        Console.WriteLine($"[1] Tồn kho ban đầu của {gao.Name}: {gao.Stock} kg. Nhận 10,000 phiếu song song...");

        Parallel.For(0, 10000, i =>
        {
            gao.Stock = gao.Stock - 1; // race condition xảy ra ở đây
        });

        Console.WriteLine($"-> [BẢN LỖI] Tồn kho còn lại: {gao.Stock} kg");
        Console.WriteLine($"   (Lý thuyết phải là 0 kg, nhưng bị sai do các luồng giẫm lên nhau!)");

        // Bản đúng: dùng lock → bắt các luồng xếp hàng → kết quả chính xác
        // Không dùng Interlocked vì Stock là decimal (Interlocked chỉ hỗ trợ int/long)
        Console.WriteLine("\n[2] Bắt đầu phiên bản CÓ ĐỒNG BỘ BẰNG LOCK...");
        var thit = new Ingredient { Name = "Thịt", Stock = 10000 };
        object khoaLock = new object();

        Parallel.For(0, 10000, i =>
        {
            lock (khoaLock)
            {
                thit.Stock = thit.Stock - 1;
            }
        });

        Console.WriteLine($"-> [BẢN ĐÚNG] Tồn kho còn lại: {thit.Stock} kg (Chuẩn xác 100% nhờ lock)");
        Console.WriteLine("================= HOÀN TẤT BÀI LAB 02 =================");
    }

    // DTOs
    public class PriceUpdateDto { public string Id { get; set; } = ""; public decimal Price { get; set; } }
    public class OrderRequest { public string PosName { get; set; } = ""; public List<OrderLineRequest> Lines { get; set; } = new(); }
    public class OrderLineRequest { public string MenuItemId { get; set; } = ""; public int Quantity { get; set; } public string Note { get; set; } = ""; }
    public class OrderResponse { public string TicketId { get; set; } = ""; public decimal FinalTotalAmount { get; set; } public string Message { get; set; } = ""; }
}