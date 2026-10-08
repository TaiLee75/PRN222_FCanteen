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
    }
}
