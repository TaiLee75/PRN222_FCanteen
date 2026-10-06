using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FCanteen.Data.Entities
{
    public class MenuItemIngredient
    {
        [Key]
        public int Id { get; set; }

        public string MenuItemId { get; set; } = string.Empty;
        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;

        public int IngredientId { get; set; }
        [ForeignKey("IngredientId")]
        public Ingredient Ingredient { get; set; } = null!;

        // Định lượng nguyên liệu cho món này
        public decimal Quantity { get; set; } 
    }
}
