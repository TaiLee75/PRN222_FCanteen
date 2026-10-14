using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty; // Loại sự kiện (OrderCreated, OrderStatusChanged, MenuItemSoldOut, UpdateConnectionCount...)

        [Required]
        public string Content { get; set; } = string.Empty;   // Nội dung chi tiết của sự kiện

        [Required]
        [MaxLength(100)]
        public string Recipient { get; set; } = string.Empty; // Đối tượng nhận (All, branch:DN:kitchen, order:123...)

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Thời điểm phát sự kiện
    }
}
