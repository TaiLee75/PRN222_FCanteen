using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities;

public class Staff
{
    [Key]
    public string StaffCode { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = null!; // Ví dụ: "Thu ngân", "Quản lý"

    [Required]
    [MaxLength(10)]
    public string WorkingBranch { get; set; } = null!; // Cơ sở làm việc
}
