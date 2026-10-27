using FCanteen.Data;
using FCanteen.WorkerService.Workers;
using Microsoft.EntityFrameworkCore;

// Đảm bảo ContentRootPath trỏ đúng thư mục file .exe ngay cả khi chạy dạng Windows Service (tránh lỗi trỏ vào C:\Windows\System32)
var hostSettings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};

var builder = Host.CreateApplicationBuilder(hostSettings);

// YC5: Cấu hình Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FCanteenWorkerService";
});

// Đăng ký DbContext (Scoped)
builder.Services.AddDbContext<FCanteenContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// YC2: Đăng ký HttpClient qua IHttpClientFactory
builder.Services.AddHttpClient("FCanteenApi", client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5050";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Đăng ký 2 Worker Service nền
builder.Services.AddHostedService<InventoryAlertWorker>();
builder.Services.AddHostedService<DailyReportWorker>();

var host = builder.Build();
host.Run();
