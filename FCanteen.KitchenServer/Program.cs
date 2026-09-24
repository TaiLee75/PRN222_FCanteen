using FCanteen.Data;
using FCanteen.Data.Entities;
using FCanteen.Repositories;
using FCanteen.Services;
using FCanteen.Services.AuditLoggers;
using FCanteen.Services.Discounts;
using FCanteen.Services.Lifetimes;
using FCanteen.Services.Notifications;
using FCanteen.Services.ReportGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Bật chế độ Development để .NET DI Container biến thành "cảnh sát" kiểm tra lỗi Captive Dependency
var settings = new HostApplicationBuilderSettings { Args = args, EnvironmentName = Environments.Development };
var builder = Host.CreateApplicationBuilder(settings);

// 1. Đăng ký CSDL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<FCanteenContext>(options => options.UseSqlServer(connectionString));

// 2. Đăng ký Repositories & Services
builder.Services.AddScoped<IMenuItemRepository, MenuItemRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

// YC2: Đăng ký các chính sách giảm giá
builder.Services.AddScoped<IDiscountPolicy, StudentDiscountPolicy>();
builder.Services.AddScoped<IDiscountPolicy, StaffDiscountPolicy>();
builder.Services.AddScoped<IDiscountPolicy, ComboDiscountPolicy>();

// YC3: Cấu hình Kênh thông báo (Bật 1 tắt 2)
//builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();
builder.Services.AddScoped<INotificationService, FileNotificationService>();
//builder.Services.AddScoped<INotificationService, EmailNotificationService>();

// YC4: Đăng ký để test Vòng đời (Lifetimes) & Captive Dependency
builder.Services.AddTransient<ITransientService, TransientService>();
builder.Services.AddScoped<IScopedService, ScopedService>();
builder.Services.AddSingleton<ISingletonService, SingletonService>();
// builder.Services.AddSingleton<BadKitchenMonitor>(); // Mở dòng này ra app sẽ văng lỗi đỏ ngay lúc Build
builder.Services.AddSingleton<GoodKitchenMonitor>();

// YC5: Đăng ký thêm các dịch vụ tiêm lẻ
builder.Services.AddTransient<IAuditLogger, ConsoleAuditLogger>();
builder.Services.AddTransient<IReportGenerator, PdfReportGenerator>();


// ==========================================
// THỰC THI (CHẠY ỨNG DỤNG)
// ==========================================
IHost host;
try
{
    // Nếu có lỗi Captive Dependency, nó sẽ nổ ngay ở dòng Build() này
    host = builder.Build();
}
catch (AggregateException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n[LOI CAPTIVE DEPENDENCY] Singleton khong duoc phep om giu DbContext (Scoped)!");
    Console.WriteLine(ex.InnerException?.Message ?? ex.Message);
    Console.ResetColor();
    return;
}

// Bảng điều khiển: các hàm test được đóng gói sẵn để nộp bài. Muốn chạy test nào thì mở comment dòng đó ra.
//await RunTestYC4_Lifetimes(host);
//await RunTestYC4_CaptiveDependency(host);
await RunTestYC5_AllInjections(host); // YC5 đã bao hàm việc chạy code của YC2 và YC3

Console.ReadLine(); // Dừng màn hình


// ==========================================
// CÁC HÀM TEST ĐƯỢC ĐÓNG GÓI SẠCH SẼ ĐỂ NỘP BÀI
// ==========================================

async Task RunTestYC5_AllInjections(IHost appHost)
{
    Console.WriteLine("\n=== TEST YC5: CHUNG MINH 4 KIEU INJECTION CUNG HOAT DONG ===");

    // Giả lập Ambient Context: Lấy thông tin nhân viên thu ngân lưu vào AsyncLocal
    StaffContext.Current = new Staff { StaffCode = "NV001", FullName = "Le Nguyen Minh Tai" };

    using var scope = appHost.Services.CreateScope();
    
    // 1. Constructor Injection (Lấy OrderService ra, nó đã tự ôm sẵn DbContext, Notification, Policies)
    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

    // 2. Property Injection (Gắn thêm Logger tùy chọn)
    orderService.AuditLogger = scope.ServiceProvider.GetService<IAuditLogger>();

    var ticket = new OrderTicket
    {
        TicketId = "TICKET_YC5_01", TotalAmount = 60000m, CreatedAt = DateTime.Now,
        TicketLines = [ new TicketLine { Id = 1, Quantity = 1, UnitPrice = 40000m }, new TicketLine { Id = 2, Quantity = 1, UnitPrice = 20000m } ]
    };

    await orderService.ProcessOrderAsync(ticket, "SinhVien");

    // 3. Method Injection (Lấy máy in PDF ra và đút thẳng vào hàm ExportReport)
    var reportGenerator = scope.ServiceProvider.GetRequiredService<IReportGenerator>();
    orderService.ExportReport(ticket, reportGenerator);
}

async Task RunTestYC4_Lifetimes(IHost appHost)
{
    Console.WriteLine("\n=== TEST YC4: SO SANH VONG DOI (LIFETIMES) ===");
    using (var scope1 = appHost.Services.CreateScope())
    {
        Console.WriteLine("--- SCOPE 1 ---");
        Console.WriteLine($"Transient 1: {scope1.ServiceProvider.GetRequiredService<ITransientService>().InstanceId}");
        Console.WriteLine($"Transient 2: {scope1.ServiceProvider.GetRequiredService<ITransientService>().InstanceId}");
        Console.WriteLine($"Scoped    1: {scope1.ServiceProvider.GetRequiredService<IScopedService>().InstanceId}");
        Console.WriteLine($"Scoped    2: {scope1.ServiceProvider.GetRequiredService<IScopedService>().InstanceId}");
        Console.WriteLine($"Singleton 1: {scope1.ServiceProvider.GetRequiredService<ISingletonService>().InstanceId}");
        Console.WriteLine($"Singleton 2: {scope1.ServiceProvider.GetRequiredService<ISingletonService>().InstanceId}");
    }
    using (var scope2 = appHost.Services.CreateScope())
    {
        Console.WriteLine("\n--- SCOPE 2 ---");
        Console.WriteLine($"Transient 3: {scope2.ServiceProvider.GetRequiredService<ITransientService>().InstanceId}");
        Console.WriteLine($"Scoped    3: {scope2.ServiceProvider.GetRequiredService<IScopedService>().InstanceId}");
        Console.WriteLine($"Singleton 3: {scope2.ServiceProvider.GetRequiredService<ISingletonService>().InstanceId}");
    }
}

async Task RunTestYC4_CaptiveDependency(IHost appHost)
{
    Console.WriteLine("\n=== TEST YC4: GIAI QUYET BRESS CAPTIVE DEPENDENCY ===");
    var goodMonitor = appHost.Services.GetRequiredService<GoodKitchenMonitor>();
    goodMonitor.CheckStatus();
}

// ----------------------------------------------------
// Các class hỗ trợ test Captive Dependency
// ----------------------------------------------------

public class BadKitchenMonitor
{
    private readonly FCanteenContext _context;
    // Bẫy lỗi: Class này là Singleton mà lại nhét DbContext (Scoped) thẳng vào Constructor
    public BadKitchenMonitor(FCanteenContext context) => _context = context;
    public void CheckStatus() => Console.WriteLine($"Ket noi DB: {_context.ContextId}");
}

public class GoodKitchenMonitor
{
    private readonly IServiceScopeFactory _scopeFactory;
    // Cách chuẩn: Chỉ cầm cái Factory tạo Scope (factory này là Singleton). 
    public GoodKitchenMonitor(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;
    
    public void CheckStatus()
    {
        // Khi cần móc vào DB, tự tay tạo một Scope ngắn hạn, xài xong vứt luôn để giải phóng RAM
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FCanteenContext>();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[GOOD MONITOR] Ket noi DB an toan! Canteen dang co {context.MenuItems.Count()} mon an.");
        Console.ResetColor();
    }
}
