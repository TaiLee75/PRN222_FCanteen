using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FCanteen.Data.Entities
{
    public class OrderStatusHistory
    {
        [Key]
        public int Id { get; set; }

        public string TicketId { get; set; } = string.Empty;
        [ForeignKey("TicketId")]
        public OrderTicket OrderTicket { get; set; } = null!;

        public int OldStatus { get; set; }
        public int NewStatus { get; set; }

        public string ChangedBy { get; set; } = string.Empty; // người thao tác
        public DateTime ChangedAt { get; set; } = DateTime.Now; // thời điểm
    }
}
