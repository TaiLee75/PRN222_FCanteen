using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Net.Http.Json;
using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            // Vừa bật server là phải đồng bộ giá luôn
            await SyncPricesAsync();
            
            // Chạy riêng 1 luồng ngầm để chờ nhập lệnh "HET món" (Gửi UDP)
            _ = HandleAdminCommandsAsync(); 

            // Lắng nghe các quầy thu ngân kết nối bằng cổng 9500 (TCP)
            TcpListener server = new TcpListener(IPAddress.Any, 9500);
            server.Start();
            Console.WriteLine("[BẾP] Máy chủ đang chạy tại cổng 9500...");

            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                Console.WriteLine($"[BẾP] Kết nối từ: {((IPEndPoint)client.Client.RemoteEndPoint!).Address}");
                
                // Quăng cho 1 Task riêng xử lý để không chặn các quầy khác
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[LỖI SERVER] {ex.Message}"); }
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        using (client) // Xài xong tự đóng kết nối
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer);
                string jsonRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                using var context = GetDbContext();
                
                // Ghi log kết nối vào nè
                context.DeviceLogs.Add(new DeviceLog { Protocol = "TCP", SourceAddress = clientIp, Content = $"Nhận JSON: {jsonRequest}" });
                await context.SaveChangesAsync();

                var request = JsonSerializer.Deserialize<OrderRequest>(jsonRequest);
                if (request == null) return;

                // Mở transaction, lỡ lỗi gì thì rollback lại hết
                using var transaction = await context.Database.BeginTransactionAsync();
                
                string newTicketId = "T" + DateTime.Now.ToString("yyyyMMddHHmmssff");
                decimal recalculatedTotal = 0;
                var ticketLines = new List<TicketLine>();

                // Tính lại tiền, lỡ client sửa giá gửi lên ăn gian
                foreach (var line in request.Lines)
                {
                    var menuItem = await context.MenuItems.FindAsync(line.MenuItemId);
                    if (menuItem != null)
                    {
                        recalculatedTotal += menuItem.Price * line.Quantity;
                        ticketLines.Add(new TicketLine
                        {
                            MenuItemId = line.MenuItemId,
                            Quantity = line.Quantity,
                            UnitPrice = menuItem.Price, // Lưu đúng giá tại thời điểm này
                            Note = line.Note
                        });
                    }
                }

                var newTicket = new OrderTicket
                {
                    TicketId = newTicketId,
                    PosName = request.PosName,
                    TotalAmount = recalculatedTotal,
                    Status = "Pending",
                    TicketLines = ticketLines
                };

                // Lưu xuống DB
                context.OrderTickets.Add(newTicket);
                await context.SaveChangesAsync();
                await transaction.CommitAsync(); // Xác nhận chốt lưu thành công

                Console.WriteLine($"[CHỜ CHẾ BIẾN] Phiếu {newTicketId} từ {request.PosName} | Tổng: {recalculatedTotal:N0}đ");

                // Gửi xác nhận lại cho quầy
                string jsonResponse = JsonSerializer.Serialize(new OrderResponse
                { TicketId = newTicketId, FinalTotalAmount = recalculatedTotal, Message = "Đã nhận phiếu thành công." });

                byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                await stream.WriteAsync(responseBytes);

                // Ghi log lúc phản hồi ra
                context.DeviceLogs.Add(new DeviceLog { Protocol = "TCP", SourceAddress = clientIp, Content = $"Phản hồi JSON: {jsonResponse}" });
                await context.SaveChangesAsync();
            }
            catch (Exception ex) { Console.WriteLine($"[LỖI CLIENT TASK] {ex.Message}"); }
        }
    }

    // Hàm gọi lấy DB context cho nhanh (dùng chung cho mấy hàm tĩnh)
    static FCanteenContext GetDbContext()
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
        return new FCanteenContext(new DbContextOptionsBuilder<FCanteenContext>().UseSqlServer(config.GetConnectionString("DefaultConnection")).Options);
    }

    // Hàm gọi API ngoài để lấy giá mới nhất (HTTP)
    static async Task SyncPricesAsync()
    {
        Console.WriteLine("\n--- YC4: ĐỒNG BỘ GIÁ TỪ SERVER TRUNG TÂM ---");
        Uri uri = new Uri("https://raw.githubusercontent.com/minhtan123/dummy/main/prices.json");
        
        // In ra thông tin DNS như yêu cầu
        Console.WriteLine($"[DNS] Scheme: {uri.Scheme}, Host: {uri.Host}, Port: {uri.Port}");
        var ips = await Dns.GetHostAddressesAsync(uri.Host);
        Console.WriteLine($"[DNS] IP Addresses: {string.Join(", ", ips.Select(ip => ip.ToString()))}");

        using var httpClient = new HttpClient();
        using var context = GetDbContext();
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync(uri);
            sw.Stop();

            // YC4 - Ghi log HTTP, nhớ nhét cả thời gian phản hồi (ElapsedMilliseconds)
            context.DeviceLogs.Add(new DeviceLog
            {
                Protocol = "HTTP",
                SourceAddress = uri.Host,
                Content = $"Đồng bộ giá. Status: {response.StatusCode}. Thời gian phản hồi: {sw.ElapsedMilliseconds}ms"
            });
            await context.SaveChangesAsync();

            // Xử lý cập nhật nếu gọi link thành công
            if (response.IsSuccessStatusCode)
            {
                var priceUpdates = await response.Content.ReadFromJsonAsync<List<PriceUpdateDto>>();
                int updateCount = 0;
                foreach (var update in priceUpdates ?? new())
                {
                    var item = await context.MenuItems.FindAsync(update.Id);
                    if (item != null && item.Price != update.Price)
                    {
                        item.Price = update.Price; // Lấy giá mới
                        updateCount++;
                    }
                }
                if (updateCount > 0) await context.SaveChangesAsync();
                Console.WriteLine($"[HTTP] Đã cập nhật giá cho {updateCount} món (Phản hồi: {sw.ElapsedMilliseconds}ms)\n");
            }
        }
        catch (Exception ex) { Console.WriteLine($"[LỖI HTTP]: {ex.Message}"); }
    }

    // Hàm đọc phím để báo hết món xuống các quầy bằng UDP
    static async Task HandleAdminCommandsAsync()
    {
        // Nhớ bật cờ cho Broadcast
        using var udpServer = new UdpClient { EnableBroadcast = true };
        var endPoint = new IPEndPoint(IPAddress.Broadcast, 9501);

        while (true)
        {
            string? input = Console.ReadLine()?.Trim();
            // Cú pháp gõ: "HET M01"
            if (input != null && input.StartsWith("HET "))
            {
                string itemId = input.Substring(4); // Lấy chữ đằng sau chữ "HET "
                using var context = GetDbContext();
                var item = await context.MenuItems.FindAsync(itemId);

                if (item != null)
                {
                    item.IsAvailable = false;
                    await context.SaveChangesAsync();
                    
                    // Ném mã món xuống cho tụi client chụp
                    byte[] bytes = Encoding.UTF8.GetBytes(itemId);
                    await udpServer.SendAsync(bytes, bytes.Length, endPoint);
                    Console.WriteLine($"[UDP BROADCAST] Đã phát thông báo hết món: {itemId}");
                }
                else Console.WriteLine($"[LỖI] Không tìm thấy món {itemId}");
            }
        }
    }

    // Mấy class chứa thông tin (DTO) gửi qua gửi lại
    public class PriceUpdateDto { public string Id { get; set; } = ""; public decimal Price { get; set; } }
    public class OrderRequest { public string PosName { get; set; } = ""; public List<OrderLineRequest> Lines { get; set; } = new(); }
    public class OrderLineRequest { public string MenuItemId { get; set; } = ""; public int Quantity { get; set; } public string Note { get; set; } = ""; }
    public class OrderResponse { public string TicketId { get; set; } = ""; public decimal FinalTotalAmount { get; set; } public string Message { get; set; } = ""; }
}