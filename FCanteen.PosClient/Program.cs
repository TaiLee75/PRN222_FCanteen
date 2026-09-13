using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// 1. Lấy tên quầy từ tham số dòng lệnh (nếu không có thì mặc định là QUAY_DEFAULT)
string posName = args.Length > 0 ? args[0] : "QUAY_DEFAULT";
Console.Title = $"Thu ngân - {posName}";
Console.WriteLine($"=== HỆ THỐNG THU NGÂN: {posName} ===");

// 2. Kết nối Database để lấy thực đơn
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

var dbOptions = new DbContextOptionsBuilder<FCanteenContext>()
    .UseSqlServer(config.GetConnectionString("DefaultConnection"))
    .Options;

using var context = new FCanteenContext(dbOptions);

// Đọc thực đơn từ database và hiển thị dạng bảng có đánh số
// Đưa danh sách thực đơn ra biến tĩnh để luồng UDP có thể tác động vào
List<MenuItem> menu = context.MenuItems
    .Where(m => m.IsAvailable)
    .OrderBy(m => m.Id) // Thêm dòng này để STT luôn đồng nhất
    .ToList();
Console.WriteLine("\n--- THỰC ĐƠN HÔM NAY ---");
Console.WriteLine($"{"STT",-5} | {"Mã món",-6} | {"Tên món",-20} | {"Giá",-10}");
Console.WriteLine(new string('-', 50));

for (int i = 0; i < menu.Count; i++)
{
    Console.WriteLine($"{i + 1,-5} | {menu[i].Id,-6} | {menu[i].Name,-20} | {menu[i].Price:N0}đ");
}

// 3. Vòng lặp nhập món ăn
var orderLines = new List<OrderLineRequest>();
decimal temporaryTotal = 0;

// --- YC4: LẮNG NGHE THÔNG BÁO HẾT MÓN (UDP) ---
_ = Task.Run(async () =>
{
    using var udpClient = new UdpClient();
    // Cấu hình ReuseAddress để 3 quầy có thể chạy trên cùng 1 máy tính mà không bị lỗi trùng cổng
    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 9501));

    while (true)
    {
        var result = await udpClient.ReceiveAsync();
        string outOfStockId = Encoding.UTF8.GetString(result.Buffer);

        // Tìm món trong danh sách và đánh dấu không chọn được nữa
        var itemToDisable = menu.FirstOrDefault(m => m.Id == outOfStockId);
        if (itemToDisable != null)
        {
            itemToDisable.IsAvailable = false;
            Console.WriteLine($"\n[CẢNH BÁO BẾP] Món {itemToDisable.Name} ({itemToDisable.Id}) vừa hết hàng! Vui lòng không order nữa.");
            Console.Write("Nhập STT món (hoặc nhấn Enter để chốt đơn): "); // In lại prompt
        }
    }
});

Console.WriteLine("\n--- TẠO ĐƠN HÀNG ---");
while (true)
{
    Console.Write("Nhập STT món (hoặc nhấn Enter để chốt đơn): ");
    string input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input)) break;

    if (int.TryParse(input, out int stt) && stt >= 1 && stt <= menu.Count)
    {
        var selectedItem = menu[stt - 1];
        if (!selectedItem.IsAvailable)
        {
            Console.WriteLine($"[LỖI] Món {selectedItem.Name} vừa được báo HẾT HÀNG! Vui lòng chọn món khác.");
            continue;
        }

        Console.Write($"Số lượng {selectedItem.Name}: ");
        if (int.TryParse(Console.ReadLine(), out int qty) && qty > 0)
        {
            Console.Write("Ghi chú (không cay, ít đá...): ");
            string note = Console.ReadLine() ?? "";

            // --- BỔ SUNG CHỐT KIỂM TRA THỨ 2 Ở ĐÂY ---
            // Đảm bảo trong lúc nhập số lượng/ghi chú, món ăn chưa bị luồng UDP báo hết
            if (!selectedItem.IsAvailable)
            {
                Console.WriteLine($"\n[LỖI] Rất tiếc! Trong lúc bạn đang nhập, món {selectedItem.Name} vừa được báo HẾT HÀNG. Đã huỷ thao tác thêm món.");
                continue; // Bỏ qua việc add vào orderLines và quay lại vòng lặp chọn món
            }

            orderLines.Add(new OrderLineRequest
            {
                MenuItemId = selectedItem.Id,
                Quantity = qty,
                Note = note
            });

            temporaryTotal += selectedItem.Price * qty;
            Console.WriteLine($"=> Đã thêm. Tạm tính: {temporaryTotal:N0}đ\n");
        }
        else
        {
            Console.WriteLine("Số lượng không hợp lệ!");
        }
    }
    else
    {
        Console.WriteLine("STT không hợp lệ, vui lòng chọn lại!");
    }
}

// 4. Gửi đơn hàng tới Máy chủ bếp (nếu có món)
if (orderLines.Count > 0)
{
    var orderRequest = new OrderRequest
    {
        PosName = posName,
        Lines = orderLines
    };

    SendOrderToServer(orderRequest);
}
else
{
    Console.WriteLine("Đơn hàng rỗng. Đã hủy.");
}

Console.ReadLine(); // Dừng màn hình

// --- CÁC HÀM HỖ TRỢ VÀ DTO ---

static void SendOrderToServer(OrderRequest orderRequest)
{
    try
    {
        Console.WriteLine("\nĐang gửi phiếu order xuống bếp...");

        // Kết nối tới server ở cổng 9500
        using TcpClient client = new TcpClient("127.0.0.1", 9500);
        using NetworkStream stream = client.GetStream();

        // Đóng gói JSON
        string jsonRequest = JsonSerializer.Serialize(orderRequest);
        byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);

        // Gửi đi
        stream.Write(requestBytes, 0, requestBytes.Length);

        // Chờ nhận phản hồi từ server
        byte[] buffer = new byte[4096];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string jsonResponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        var response = JsonSerializer.Deserialize<OrderResponse>(jsonResponse);
        if (response != null)
        {
            Console.WriteLine("\n=== XÁC NHẬN TỪ BẾP ===");
            Console.WriteLine($"Mã phiếu: {response.TicketId}");
            Console.WriteLine($"Tổng tiền thu khách: {response.FinalTotalAmount:N0}đ");
            Console.WriteLine($"Trạng thái: {response.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[LỖI KẾT NỐI SERVER]: {ex.Message}. Vui lòng kiểm tra Máy chủ bếp đã chạy chưa.");
    }
}

// Các class DTO phải khớp cấu trúc với Server
public class OrderRequest
{
    public string PosName { get; set; } = string.Empty;
    public List<OrderLineRequest> Lines { get; set; } = new();
}

public class OrderLineRequest
{
    public string MenuItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class OrderResponse
{
    public string TicketId { get; set; } = string.Empty;
    public decimal FinalTotalAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}