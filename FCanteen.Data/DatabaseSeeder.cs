using FCanteen.Data.Entities;
using System.Diagnostics;

namespace FCanteen.Data
{
    /// <summary>
    /// YC1: Sinh dữ liệu lớn (50k phiếu, ~200k dòng chi tiết) trải đều 6 tháng, 3 cơ sở.
    /// Tối ưu: tắt Tracking + lưu theo lô để không tràn RAM.
    /// </summary>
    public static class DatabaseSeeder
    {
        public static async Task SeedLargeDataAsync(FCanteenContext context)
        {
            // Tự động dọn dẹp nếu lỡ tay xóa TicketLines mà quên xóa OrderTickets
            if (context.OrderTickets.Any() && !context.TicketLines.Any())
            {
                Console.WriteLine("Phat hien du lieu bi lech, dang don dep ban nhap cu...");
                context.OrderTickets.RemoveRange(context.OrderTickets);
                await context.SaveChangesAsync();
            }

            // Nếu đã đủ 50k rồi thì bỏ qua (giữ lại data cũ của Lab 01)
            if (context.OrderTickets.Count() >= 50000) return;

            Console.WriteLine("Dang sinh du lieu lon (50,000 phieu), vui long cho...");
            var stopwatch = Stopwatch.StartNew();

            // Tắt tracking → EF không giám sát từng object → tiết kiệm RAM
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            var random = new Random();
            var branches = new[] { "B01", "B02", "B03" };
            var menuItems = context.MenuItems.ToList();

            DateTime endDate = DateTime.Now;
            DateTime startDate = endDate.AddMonths(-6);
            int totalDays = (endDate - startDate).Days;

            int targetTickets = 50000;
            int batchSize = 10000;

            var ticketBatch = new List<OrderTicket>();
            var lineBatch = new List<TicketLine>();

            for (int i = 1; i <= targetTickets; i++)
            {
                var randomDate = startDate.AddDays(random.Next(totalDays));

                // 60% rơi vào giờ cao điểm 11h-13h, 40% còn lại random cả ngày
                int hour = (random.Next(100) < 60) ? random.Next(11, 14) : random.Next(7, 21);
                var createdAt = new DateTime(randomDate.Year, randomDate.Month, randomDate.Day, hour, random.Next(60), random.Next(60));

                var ticketId = $"T{createdAt:yyMMddHHmmss}{i:D5}";
                var ticket = new OrderTicket
                {
                    TicketId = ticketId,
                    PosName = $"POS_01",
                    CreatedAt = createdAt,
                    Status = (int)OrderStatus.Delivered,
                    BranchCode = branches[random.Next(branches.Length)],
                    TotalAmount = 0
                };

                // Mỗi phiếu 2-6 dòng → tổng khoảng 200k dòng
                int lineCount = random.Next(2, 7);
                decimal totalAmount = 0;
                for (int j = 0; j < lineCount; j++)
                {
                    var item = menuItems[random.Next(menuItems.Count)];
                    int quantity = random.Next(1, 4);
                    lineBatch.Add(new TicketLine
                    {
                        OrderTicketId = ticketId,
                        MenuItemId = item.Id,
                        Quantity = quantity,
                        UnitPrice = item.Price
                    });
                    totalAmount += (item.Price * quantity);
                }

                ticket.TotalAmount = totalAmount;
                ticketBatch.Add(ticket);

                // Lưu theo lô 10k, rồi xóa khỏi RAM
                if (i % batchSize == 0 || i == targetTickets)
                {
                    await context.OrderTickets.AddRangeAsync(ticketBatch);
                    await context.TicketLines.AddRangeAsync(lineBatch);
                    await context.SaveChangesAsync();

                    Console.WriteLine($"Đã mồi xong {i} phiếu...");

                    ticketBatch.Clear();
                    lineBatch.Clear();
                    context.ChangeTracker.Clear();
                }
            }

            // Seed vài nguyên liệu mẫu cho YC5
            if (!context.Ingredients.Any())
            {
                context.Ingredients.AddRange(
                    new Ingredient { Name = "Gạo", Unit = "Kg", Stock = 500, WarningThreshold = 50 },
                    new Ingredient { Name = "Thịt", Unit = "Kg", Stock = 200, WarningThreshold = 30 }
                );
                await context.SaveChangesAsync();
            }

            context.ChangeTracker.AutoDetectChangesEnabled = true;
            stopwatch.Stop();
            Console.WriteLine($"Hoàn tất sinh dữ liệu trong {stopwatch.ElapsedMilliseconds} ms.");
        }

        public static async Task SeedLab08DataAsync(FCanteenContext context)
        {
            // 1. Seed nguyên liệu mẫu (cả tồn kho an toàn và dưới ngưỡng báo động)
            if (!context.Ingredients.Any())
            {
                var ingredients = new List<Ingredient>
                {
                    new Ingredient { Name = "Gạo ST25", Unit = "Kg", Stock = 150, WarningThreshold = 50, UnitPrice = 25000, SupplierId = 1 },
                    new Ingredient { Name = "Thịt heo ba chỉ", Unit = "Kg", Stock = 40, WarningThreshold = 20, UnitPrice = 110000, SupplierId = 1 },
                    new Ingredient { Name = "Thịt bò Úc", Unit = "Kg", Stock = 8, WarningThreshold = 15, UnitPrice = 240000, SupplierId = 1 }, // Sắp hết (8 <= 15)
                    new Ingredient { Name = "Trứng gà ta", Unit = "Quả", Stock = 15, WarningThreshold = 50, UnitPrice = 3000, SupplierId = 2 }, // Sắp hết (15 <= 50)
                    new Ingredient { Name = "Dầu ăn Simply", Unit = "Lít", Stock = 4, WarningThreshold = 10, UnitPrice = 48000, SupplierId = 2 }, // Sắp hết (4 <= 10)
                    new Ingredient { Name = "Hành lá", Unit = "Kg", Stock = 1.5m, WarningThreshold = 5, UnitPrice = 35000, SupplierId = 2 }, // Sắp hết (1.5 <= 5)
                    new Ingredient { Name = "Gia vị hạt nêm", Unit = "Gói", Stock = 80, WarningThreshold = 20, UnitPrice = 12000, SupplierId = 2 }
                };

                await context.Ingredients.AddRangeAsync(ingredients);
                await context.SaveChangesAsync();
                Console.WriteLine("Đã seed thành công 7 nguyên liệu mẫu cho Lab 08.");
            }

            // 2. Seed đơn hàng mẫu ngày hôm qua để Worker 2 (chốt báo cáo) có dữ liệu tổng hợp
            var yesterday = DateTime.Today.AddDays(-1);
            bool hasYesterdayOrders = context.OrderTickets.Any(o => o.CreatedAt.Date == yesterday.Date);
            if (!hasYesterdayOrders)
            {
                var menuItems = context.MenuItems.Take(5).ToList();
                if (menuItems.Any())
                {
                    var tickets = new List<OrderTicket>();
                    var lines = new List<TicketLine>();

                    for (int i = 1; i <= 6; i++)
                    {
                        var ticketId = $"T{yesterday:yyMMdd}{i:D4}";
                        // Đơn 5 là đơn huỷ (Cancelled = 5), các đơn khác là Delivered (4)
                        int status = (i == 5) ? (int)OrderStatus.Cancelled : (int)OrderStatus.Delivered;
                        var orderTime = yesterday.AddHours(10 + i).AddMinutes(15 * i);

                        var ticket = new OrderTicket
                        {
                            TicketId = ticketId,
                            BranchCode = "DN",
                            PosName = "POS_01",
                            Status = status,
                            CreatedAt = orderTime,
                            TotalAmount = 0
                        };

                        decimal total = 0;
                        // Món 0 bán chạy nhất (cho nhiều số lượng)
                        var item1 = menuItems[0];
                        lines.Add(new TicketLine { OrderTicketId = ticketId, MenuItemId = item1.Id, Quantity = 2 + i, UnitPrice = item1.Price });
                        total += item1.Price * (2 + i);

                        if (menuItems.Count > 1)
                        {
                            var item2 = menuItems[1];
                            lines.Add(new TicketLine { OrderTicketId = ticketId, MenuItemId = item2.Id, Quantity = 1, UnitPrice = item2.Price });
                            total += item2.Price * 1;
                        }

                        ticket.TotalAmount = total;
                        tickets.Add(ticket);
                    }

                    await context.OrderTickets.AddRangeAsync(tickets);
                    await context.TicketLines.AddRangeAsync(lines);
                    await context.SaveChangesAsync();
                    Console.WriteLine("Đã seed thành công 6 đơn hàng mẫu của ngày hôm qua cho Lab 08.");
                }
            }
        }
    }
}
