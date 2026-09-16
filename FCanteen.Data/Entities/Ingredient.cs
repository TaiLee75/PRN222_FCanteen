using System.ComponentModel.DataAnnotations;

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
    }
}
