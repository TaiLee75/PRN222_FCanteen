using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Data
{
    public class FCanteenContext : DbContext
    {
        public FCanteenContext(DbContextOptions<FCanteenContext> options) : base(options) { }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<OrderTicket> OrderTickets { get; set; }
        public DbSet<TicketLine> TicketLines { get; set; }
        public DbSet<DeviceLog> DeviceLogs { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }           // YC1 - bảng nguyên liệu
        public DbSet<DailySettlement> DailySettlements { get; set; } // YC1 - bảng chốt ca

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ép kiểu decimal(18,2) cho các cột tiền và số lượng
            modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<OrderTicket>().Property(o => o.TotalAmount).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<TicketLine>().Property(t => t.UnitPrice).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Ingredient>().Property(i => i.Stock).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Ingredient>().Property(i => i.WarningThreshold).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<DailySettlement>().Property(d => d.TotalRevenue).HasColumnType("decimal(18, 2)");

            // Seed 15 món ăn mặc định
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
