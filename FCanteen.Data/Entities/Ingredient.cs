using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace FCanteen.Data.Entities
{
    // Bảng nguyên liệu - phục vụ YC5 (mô phỏng race condition trừ tồn kho)
    public class Ingredient
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Stock { get; set; }
        public decimal WarningThreshold { get; set; }

        // Lab 4 additions
        public decimal UnitPrice { get; set; } // Đơn giá nguyên liệu để tính giá vốn

        public int? SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        public ICollection<MenuItemIngredient> MenuItemIngredients { get; set; } = new List<MenuItemIngredient>();
    }
}
