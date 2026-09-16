using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    // Bảng chốt ca cuối ngày - lưu kết quả tính toán + thời gian benchmark
    public class DailySettlement
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public decimal TotalRevenue { get; set; }
        public long CalculationTimeMs { get; set; } // tính bằng ms
    }
}
