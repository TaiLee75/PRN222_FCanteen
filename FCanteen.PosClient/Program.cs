using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Console.InputEncoding = Encoding.UTF8;

// Nhận tham số từ args, nếu không nhập gì thì lấy QUAY_DEFAULT (YC3)
string posName = args.Length > 0 ? args[0] : "QUAY_DEFAULT";
Console.Title = $"Thu ngân - {posName}";
Console.WriteLine($"=== HỆ THỐNG THU NGÂN: {posName} ===");

// Lấy chuỗi kết nối từ file appsettings
var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
using var context = new FCanteenContext(new DbContextOptionsBuilder<FCanteenContext>().UseSqlServer(config.GetConnectionString("DefaultConnection")).Options);

// Lấy mấy món còn bán
var menu = context.MenuItems.Where(m => m.IsAvailable).OrderBy(m => m.Id).ToList();
Console.WriteLine("\n--- THỰC ĐƠN HÔM NAY ---");
Console.WriteLine($"{"STT",-5} | {"Mã món",-6} | {"Tên món",-20} | {"Giá",-10}");
Console.WriteLine(new string('-', 50));
for (int i = 0; i < menu.Count; i++)
    Console.WriteLine($"{i + 1,-5} | {menu[i].Id,-6} | {menu[i].Name,-20} | {menu[i].Price:N0}đ");

var orderLines = new List<OrderLineRequest>();
decimal temporaryTotal = 0;

// Chạy 1 luồng ngầm để hứng tin báo UDP từ bếp (khi bếp gõ lệnh HET)
_ = Task.Run(async () =>
{
    using var udpClient = new UdpClient();
    // Bật ReuseAddress để mình mở 3 cái cmd chạy chung 1 máy thì khỏi bị lỗi đụng cổng
    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 9501));
    while (true)
    {
        var result = await udpClient.ReceiveAsync();
        string outOfStockId = Encoding.UTF8.GetString(result.Buffer);
        
        // Cập nhật lại list menu bên mình
        var itemToDisable = menu.FirstOrDefault(m => m.Id == outOfStockId);
        if (itemToDisable != null)
        {
            itemToDisable.IsAvailable = false;
            Console.WriteLine($"\n[CẢNH BÁO BẾP] Món {itemToDisable.Name} ({itemToDisable.Id}) vừa hết hàng!");
            Console.Write("Nhập STT món (hoặc Enter chốt đơn): "); // in lại cái dấu nhắc cho đỡ trôi màn hình
        }
    }
});

Console.WriteLine("\n--- TẠO ĐƠN HÀNG ---");
while (true)
{
    Console.Write("Nhập STT món (hoặc Enter chốt đơn): ");
    // Nếu rỗng hoặc gõ bậy bạ là break chốt đơn luôn
    if (!int.TryParse(Console.ReadLine()?.Trim(), out int stt) || stt < 1 || stt > menu.Count) break;

    var selectedItem = menu[stt - 1];
    
    // Nếu món bị gạch IsAvailable = false từ luồng UDP nãy thì từ chối
    if (!selectedItem.IsAvailable)
    {
        Console.WriteLine($"[LỖI] Món {selectedItem.Name} đã HẾT HÀNG!");
        continue;
    }

    Console.Write($"Số lượng {selectedItem.Name}: ");
    if (int.TryParse(Console.ReadLine(), out int qty) && qty > 0)
    {
        Console.Write("Ghi chú: ");
        string note = Console.ReadLine() ?? "";

        // Chặn lần cuối, lỡ lúc đang nhập ghi chú lâu quá bếp nó hết thật
        if (!selectedItem.IsAvailable)
        {
            Console.WriteLine($"\n[LỖI] Trong lúc nhập, món {selectedItem.Name} vừa báo HẾT HÀNG.");
            continue;
        }

        // Bỏ vô giỏ
        orderLines.Add(new OrderLineRequest { MenuItemId = selectedItem.Id, Quantity = qty, Note = note });
        temporaryTotal += selectedItem.Price * qty;
        Console.WriteLine($"=> Đã thêm. Tạm tính: {temporaryTotal:N0}đ\n");
    }
    else Console.WriteLine("Số lượng không hợp lệ!");
}

// Nếu có món thì đẩy xuống server thôi
if (orderLines.Any()) await SendOrderToServerAsync(new OrderRequest { PosName = posName, Lines = orderLines });
else Console.WriteLine("Đơn hàng rỗng. Đã hủy.");

Console.ReadLine();

// Hàm xử lý việc gửi mảng JSON xuống TCP cổng 9500 (YC3)
async Task SendOrderToServerAsync(OrderRequest orderRequest)
{
    try
    {
        Console.WriteLine("\nĐang gửi phiếu order xuống bếp...");
        using TcpClient client = new TcpClient("127.0.0.1", 9500);
        using NetworkStream stream = client.GetStream();

        // Ép sang bytes rùi gửi
        byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderRequest));
        await stream.WriteAsync(requestBytes);

        // Chờ nhận kết quả từ bếp báo mã đơn
        byte[] buffer = new byte[4096];
        int bytesRead = await stream.ReadAsync(buffer);
        var response = JsonSerializer.Deserialize<OrderResponse>(Encoding.UTF8.GetString(buffer, 0, bytesRead));

        if (response != null)
        {
            Console.WriteLine("\n=== XÁC NHẬN TỪ BẾP ===");
            Console.WriteLine($"Mã phiếu: {response.TicketId}\nTổng tiền: {response.FinalTotalAmount:N0}đ\nTrạng thái: {response.Message}");
        }
    }
    catch (Exception ex) { Console.WriteLine($"[LỖI KẾT NỐI]: {ex.Message}"); }
}

// DTO phải chuẩn y như con Server (YC3)
public class OrderRequest { public string PosName { get; set; } = ""; public List<OrderLineRequest> Lines { get; set; } = new(); }
public class OrderLineRequest { public string MenuItemId { get; set; } = ""; public int Quantity { get; set; } public string Note { get; set; } = ""; }
public class OrderResponse { public string TicketId { get; set; } = ""; public decimal FinalTotalAmount { get; set; } public string Message { get; set; } = ""; }