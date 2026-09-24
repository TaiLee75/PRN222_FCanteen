using FCanteen.Data.Entities;

namespace FCanteen.Services.Discounts;

public interface IDiscountPolicy
{
    // Thứ tự ưu tiên (Số càng nhỏ càng áp dụng trước)
    int Priority { get; }

    // Tên của chính sách để lát ghi vào Log
    string PolicyName { get; }

    // Kiểm tra xem bill này có thỏa điều kiện áp dụng không
    bool IsApplicable(OrderTicket ticket, string customerRole);

    // Tính toán số tiền được giảm
    decimal CalculateDiscount(OrderTicket ticket);
}
