using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Data
{
    public class FCanteenContext : DbContext
    {
        public FCanteenContext(DbContextOptions<FCanteenContext> options) : base(options) { }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<OrderTicket> OrderTickets { get; set; }
        public DbSet<TicketLine> TicketLines { get; set; }
        public DbSet<DeviceLog> DeviceLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình kiểu dữ liệu decimal cho các cột tiền tệ
            modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<OrderTicket>().Property(o => o.TotalAmount).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<TicketLine>().Property(t => t.UnitPrice).HasColumnType("decimal(18, 2)");

            // Tự động seed 15 món ăn theo yêu cầu (chỉ dùng HasData, không dùng INSERT thủ công)
            var menuItems = new List<MenuItem>();
            for (int i = 1; i <= 15; i++)
            {
                menuItems.Add(new MenuItem
                {
                    Id = $"M{i:D2}",
                    Name = $"Món ăn {i}",
                    Price = 15000 + (i * 1000),
                    Unit = "Phần",
                    IsAvailable = true
                });
            }
            modelBuilder.Entity<MenuItem>().HasData(menuItems);
        }
    }
}
