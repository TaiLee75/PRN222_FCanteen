using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class OrderTicket
    {
        [Key]
        public string TicketId { get; set; } = string.Empty; // Mã phiếu
        public string PosName { get; set; } = string.Empty; // Tên quầy gửi
        public decimal TotalAmount { get; set; } // Tổng tiền
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending"; // Trạng thái

        // Navigation property
        public ICollection<TicketLine> TicketLines { get; set; } = new List<TicketLine>();
    }
}