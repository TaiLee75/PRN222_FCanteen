using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class OrderTicket
    {
        [Key]
        public string TicketId { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty; // B01, B02, B03
        public string PosName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Status { get; set; } = (int)OrderStatus.Pending;

        public ICollection<TicketLine> TicketLines { get; set; } = new List<TicketLine>();
        public ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();
    }
}