using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FCanteen.Data.Entities;

public class DiscountPolicyLog
{
    [Key]
    public int Id { get; set; }
    public string OrderTicketId { get; set; } = null!;
    public string PolicyName { get; set; } = null!;
    
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }
    public DateTime AppliedAt { get; set; }
}
