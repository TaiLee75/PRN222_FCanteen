using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
        
        // Lab 4 additions
        public decimal CostPrice { get; set; } // Giá vốn tính từ định lượng nguyên liệu
        
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public ICollection<MenuItemIngredient> MenuItemIngredients { get; set; } = new List<MenuItemIngredient>();
    }
}