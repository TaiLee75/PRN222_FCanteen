using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FCanteen.Data.Entities
{
    public class TicketLine
    {
        [Key]
        public int Id { get; set; }

        public string OrderTicketId { get; set; } = string.Empty;
        [ForeignKey("OrderTicketId")]
        public OrderTicket OrderTicket { get; set; } = null!;

        public string MenuItemId { get; set; } = string.Empty;
        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Giá tại thời điểm bán
        public string? Note { get; set; } // Ghi chú của khách
    }
}