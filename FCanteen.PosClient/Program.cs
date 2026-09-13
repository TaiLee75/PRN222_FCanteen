using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FCanteen.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
var menu = context.MenuItems.Where(m => m.IsAvailable).ToList();
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

Console.WriteLine("\n--- TẠO ĐƠN HÀNG ---");
while (true)
{
    Console.Write("Nhập STT món (hoặc nhấn Enter để chốt đơn): ");
    string input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input)) break;

    if (int.TryParse(input, out int stt) && stt >= 1 && stt <= menu.Count)
    {
        var selectedItem = menu[stt - 1];

        Console.Write($"Số lượng {selectedItem.Name}: ");
        if (int.TryParse(Console.ReadLine(), out int qty) && qty > 0)
        {
            Console.Write("Ghi chú (không cay, ít đá...): ");
            string note = Console.ReadLine() ?? "";

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