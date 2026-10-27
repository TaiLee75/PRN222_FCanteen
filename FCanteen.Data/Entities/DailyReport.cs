using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class DailyReport
    {
        [Key]
        public int Id { get; set; }

        public DateTime ReportDate { get; set; }

        public decimal TotalRevenue { get; set; }

        public int TotalOrders { get; set; }

        public string TopSellingItem { get; set; } = string.Empty;

        public int TopSellingQuantity { get; set; }

        public int CancelledOrdersCount { get; set; }

        // Tỉ lệ huỷ đơn (tính theo %, ví dụ 15.5%)
        public decimal CancellationRate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
