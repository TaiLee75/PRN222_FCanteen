using FCanteen.Data;
using FCanteen.Data.Entities;
using FCanteen.Repositories;
using FCanteen.Services.AuditLoggers;
using FCanteen.Services.Discounts;
using FCanteen.Services.Notifications;
using FCanteen.Services.ReportGenerators;

namespace FCanteen.Services;

public interface IOrderService
{
    Task ProcessOrderAsync(OrderTicket ticket, string customerRole);
    void ExportReport(OrderTicket ticket, IReportGenerator reportGenerator);
    IAuditLogger? AuditLogger { get; set; }
}

public class OrderService : IOrderService
{
    // KIỂU 1: CONSTRUCTOR INJECTION
    // Dùng cho những Dependency cốt lõi. Không có DbContext hay DiscountPolicy thì nghiệp vụ chết ngay.
    // Container sẽ tự động tìm và nhét các đối tượng này vào lúc khởi tạo Service.
    private readonly IEnumerable<IDiscountPolicy> _discountPolicies;
    private readonly FCanteenContext _context;
    private readonly INotificationService _notificationService;

    public OrderService(
        IEnumerable<IDiscountPolicy> discountPolicies, 
        FCanteenContext context, 
        INotificationService notificationService)
    {
        _discountPolicies = discountPolicies;
        _context = context;
        _notificationService = notificationService;
    }

    // KIỂU 2: PROPERTY INJECTION
    // Dùng cho những tính năng phụ (Optional). 
    // Gắn vào thì ghi log, quên không gắn (bị null) thì dùng dấu '?.' app vẫn chạy mượt mà, không sập.
    public IAuditLogger? AuditLogger { get; set; }

    public async Task ProcessOrderAsync(OrderTicket ticket, string customerRole)
    {
        // KIỂU 4: AMBIENT CONTEXT (Dùng AsyncLocal)
        // Mình lấy thẳng thông tin nhân viên đang đăng nhập từ Context của luồng hiện tại.
        // Đỡ phải thêm tham số 'Staff' lằng nhằng đi qua từng tầng code.
        var currentStaff = StaffContext.Current;
        if (currentStaff != null)
        {
            Console.WriteLine($"[StaffContext] Nhan vien thao tac: {currentStaff.FullName} ({currentStaff.StaffCode})");
        }

        decimal currentTotal = ticket.TotalAmount;
        var orderedPolicies = _discountPolicies.OrderBy(p => p.Priority);

        // Chạy qua các chính sách giảm giá (YC2: Open/Closed Principle)
        foreach (var policy in orderedPolicies)
        {
            if (policy.IsApplicable(ticket, customerRole))
            {
                decimal discountValue = policy.CalculateDiscount(ticket);
                currentTotal -= discountValue;

                var log = new DiscountPolicyLog
                {
                    OrderTicketId = ticket.TicketId,
                    PolicyName = policy.PolicyName,
                    DiscountAmount = discountValue,
                    AppliedAt = DateTime.Now
                };
                _context.DiscountPolicyLogs.Add(log);

                Console.WriteLine($"[Giam Gia] {ticket.TicketId} - Ap dung: {policy.PolicyName}. Tru: {discountValue}d");
            }
        }

        ticket.TotalAmount = currentTotal;
        await _context.SaveChangesAsync();

        // Gửi thông báo (YC3: Dependency Inversion)
        await _notificationService.SendAsync($"Da xu ly don {ticket.TicketId} - Tong: {ticket.TotalAmount}d");

        // Gọi Property Injection để ghi log kiểm toán
        AuditLogger?.Log($"Ghi nhan kiem toan: Don hang {ticket.TicketId} xu ly boi {currentStaff?.FullName ?? "He thong"}");
    }

    // KIỂU 3: METHOD INJECTION
    // Chức năng in báo cáo rất hiếm khi xài, nhét vào Constructor thì lãng phí RAM của hệ thống.
    // Do đó, chỉ khi nào gọi hàm này mình mới truyền IReportGenerator vào qua tham số.
    public void ExportReport(OrderTicket ticket, IReportGenerator reportGenerator)
    {
        reportGenerator.GenerateReport(ticket);
    }
}
