using FCanteen.Data.Entities;

namespace FCanteen.Services.Discounts;

public class ComboDiscountPolicy : IDiscountPolicy
{
    public int Priority => 3;
    public string PolicyName => "Giam 5.000d Combo Chinh + Nuoc";

    public bool IsApplicable(OrderTicket ticket, string customerRole)
    {
        // Để đơn giản bài lab: Cứ mua trên 1 món là mặc định giảm 5k combo
        return ticket.TicketLines != null && ticket.TicketLines.Count >= 2;
    }

    public decimal CalculateDiscount(OrderTicket ticket)
    {
        return 5000m; // Trừ cứng 5k
    }
}
