using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}
