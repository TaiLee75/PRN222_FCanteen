using FCanteen.Data.Entities;

namespace FCanteen.Services.Discounts;

public class StudentDiscountPolicy : IDiscountPolicy
{
    public int Priority => 1; // Áp dụng đầu tiên
    public string PolicyName => "Giam 10% cho Sinh Vien";

    public bool IsApplicable(OrderTicket ticket, string customerRole)
    {
        return customerRole.Equals("SinhVien", StringComparison.OrdinalIgnoreCase);
    }

    public decimal CalculateDiscount(OrderTicket ticket)
    {
        return ticket.TotalAmount * 0.1m;
    }
}
