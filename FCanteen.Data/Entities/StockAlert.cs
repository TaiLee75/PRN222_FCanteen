using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FCanteen.Data.Entities
{
    public class StockAlert
    {
        [Key]
        public int Id { get; set; }

        public int IngredientId { get; set; }

        [ForeignKey("IngredientId")]
        public Ingredient? Ingredient { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public decimal CurrentStock { get; set; }

        public decimal WarningThreshold { get; set; }

        public DateTime AlertTime { get; set; } = DateTime.Now;

        // Trạng thái đã xử lý (bổ sung nguyên liệu) hay chưa. Chưa xử lý = false
        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        public string? Note { get; set; }
    }
}
