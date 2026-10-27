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

        public DbSet<Staff> Staffs { get; set; }

        public DbSet<DiscountPolicyLog> DiscountPolicyLogs { get; set; }
        
        // Lab 4 DbSets
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<MenuItemIngredient> MenuItemIngredients { get; set; }

        // Lab 06
        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

        // Lab 07 - Bảng thứ 15 lưu mọi sự kiện SignalR đã phát
        public DbSet<Notification> Notifications { get; set; }

        // Lab 08 - Worker Service (StockAlert & DailyReport)
        public DbSet<StockAlert> StockAlerts { get; set; }
        public DbSet<DailyReport> DailyReports { get; set; }

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

            // Lab 4 Decimal Types
            modelBuilder.Entity<MenuItem>().Property(m => m.CostPrice).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Ingredient>().Property(i => i.UnitPrice).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<MenuItemIngredient>().Property(m => m.Quantity).HasColumnType("decimal(18, 2)");

            // Lab 08 Decimal Types
            modelBuilder.Entity<StockAlert>().Property(s => s.CurrentStock).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<StockAlert>().Property(s => s.WarningThreshold).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<DailyReport>().Property(d => d.TotalRevenue).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<DailyReport>().Property(d => d.CancellationRate).HasColumnType("decimal(18, 2)");

            // Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Món chính" },
                new Category { Id = 2, Name = "Món phụ" },
                new Category { Id = 3, Name = "Đồ uống" },
                new Category { Id = 4, Name = "Tráng miệng" }
            );

            // Seed Suppliers
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = 1, Name = "Nhà cung cấp Vissan", Phone = "0123456789" },
                new Supplier { Id = 2, Name = "CoopMart", Phone = "0987654321" }
            );

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
