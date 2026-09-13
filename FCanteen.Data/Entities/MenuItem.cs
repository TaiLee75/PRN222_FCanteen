using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class MenuItem
    {
        [Key]
        public string Id { get; set; } = string.Empty; // Mã món
        public string Name { get; set; } = string.Empty; // Tên món
        public decimal Price { get; set; } // Giá bán
        public string Unit { get; set; } = string.Empty; // Đơn vị tính
        public bool IsAvailable { get; set; } = true; // Trạng thái còn bán
    }
}