using FCanteen.KitchenServer.Components;
using FCanteen.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// YC1: Đăng ký DbContextFactory thay vì DbContext
// Giải thích cho báo cáo: Blazor Server duy trì một kết nối dài hạn (circuit) với trình duyệt. 
// Nếu dùng AddDbContext (Scoped), DbContext sẽ sống rất lâu và dễ gây lỗi "A second operation was started on this context before a previous operation completed"
// khi Timer ngầm và User thao tác đồng thời. AddDbContextFactory cho phép tạo DbContext riêng biệt mỗi khi cần.
builder.Services.AddDbContextFactory<FCanteenContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
