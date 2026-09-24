using FCanteen.Data.Entities;

namespace FCanteen.Services.Discounts;

public class StaffDiscountPolicy : IDiscountPolicy
{
    public int Priority => 2;
    public string PolicyName => "Giam 15% cho Giang Vien/Nhan Vien";

    public bool IsApplicable(OrderTicket ticket, string customerRole)
    {
        return customerRole.Equals("GiangVien", StringComparison.OrdinalIgnoreCase)
            || customerRole.Equals("NhanVien", StringComparison.OrdinalIgnoreCase);
    }

    public decimal CalculateDiscount(OrderTicket ticket)
    {
        return ticket.TotalAmount * 0.15m;
    }
}
