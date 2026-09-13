using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        TcpListener server = null;
        try
        {
            // Lắng nghe cổng 9500
            int port = 9500;
            server = new TcpListener(IPAddress.Any, port);
            // --- YC4: ĐỒNG BỘ GIÁ BẰNG HTTP CLIENT & DNS ---
            await SyncPricesAsync();

            // --- TẠO LUỒNG NHẬP LỆNH BÁO HẾT MÓN (UDP) ---
            _ = Task.Run(() => HandleAdminCommandsAsync());
            server.Start();
            Console.WriteLine($"[BẾP] Máy chủ đang chạy tại cổng {port}...");

            while (true)
            {
                // Chờ kết nối từ quầy
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine($"[BẾP] Đã kết nối với quầy: {((IPEndPoint)client.Client.RemoteEndPoint!).Address}");

                // Phục vụ đồng thời nhiều quầy, mỗi kết nối một Task riêng
                Task.Run(() => HandleClient(client));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LỖI SERVER] {ex.Message}");
        }
    }

    static void HandleClient(TcpClient client)
    {
        try
        {
            using NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string jsonRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

            using var context = GetDbContext();

            // Ghi DeviceLog kết nối vào
            context.DeviceLogs.Add(new DeviceLog
            {
                Protocol = "TCP",
                SourceAddress = clientIp,
                Content = $"Nhận JSON: {jsonRequest}",
                Timestamp = DateTime.Now
            });
            context.SaveChanges();

            // Giải mã JSON
            var request = JsonSerializer.Deserialize<OrderRequest>(jsonRequest);
            if (request != null)
            {
                // Xử lý Transaction
                using var transaction = context.Database.BeginTransaction();
                try
                {
                    string newTicketId = "T" + DateTime.Now.ToString("yyyyMMddHHmmssff");
                    decimal recalculatedTotal = 0;
                    var ticketLines = new List<TicketLine>();

                    foreach (var lineReq in request.Lines)
                    {
                        // Đọc giá từ database để tính lại (không tin client gửi lên)
                        var menuItem = context.MenuItems.Find(lineReq.MenuItemId);
                        if (menuItem != null)
                        {
                            decimal linePrice = menuItem.Price;
                            recalculatedTotal += linePrice * lineReq.Quantity;

                            ticketLines.Add(new TicketLine
                            {
                                MenuItemId = lineReq.MenuItemId,
                                Quantity = lineReq.Quantity,
                                UnitPrice = linePrice, // Lưu giá thời điểm bán
                                Note = lineReq.Note
                            });
                        }
                    }

                    var newTicket = new OrderTicket
                    {
                        TicketId = newTicketId,
                        PosName = request.PosName,
                        TotalAmount = recalculatedTotal, // Ghi nhận tổng tiền đã tính lại
                        Status = "Pending",
                        TicketLines = ticketLines
                    };

                    context.OrderTickets.Add(newTicket);
                    context.SaveChanges();
                    transaction.Commit();

                    // In ra màn hình phiếu đang chờ chế biến
                    Console.WriteLine($"[CHỜ CHẾ BIẾN] Phiếu {newTicketId} từ {request.PosName} | Tổng: {recalculatedTotal:N0}đ");

                    // Trả về cho quầy xác nhận
                    var responseObj = new OrderResponse
                    {
                        TicketId = newTicketId,
                        FinalTotalAmount = recalculatedTotal,
                        Message = "Đã nhận phiếu thành công."
                    };
                    string jsonResponse = JsonSerializer.Serialize(responseObj);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                    stream.Write(responseBytes, 0, responseBytes.Length);

                    // Ghi DeviceLog kết nối ra
                    context.DeviceLogs.Add(new DeviceLog
                    {
                        Protocol = "TCP",
                        SourceAddress = clientIp,
                        Content = $"Phản hồi JSON: {jsonResponse}",
                        Timestamp = DateTime.Now
                    });
                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"[LỖI TRANSACTION] {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LỖI CLIENT TASK] {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    // Hàm hỗ trợ khởi tạo DbContext cho Console App
    static FCanteenContext GetDbContext()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<FCanteenContext>();
        optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));

        return new FCanteenContext(optionsBuilder.Options);
    }

    // Hàm đồng bộ giá
    static async Task SyncPricesAsync()
    {
        string url = "https://raw.githubusercontent.com/minhtan123/dummy/main/prices.json";
        Console.WriteLine("\n--- YC4: ĐỒNG BỘ GIÁ TỪ SERVER TRUNG TÂM ---");

        // 1. Phân tích Uri và Dns
        Uri uri = new Uri(url);
        Console.WriteLine($"[DNS] Scheme: {uri.Scheme}, Host: {uri.Host}, Port: {uri.Port}");

        var ips = await System.Net.Dns.GetHostAddressesAsync(uri.Host);
        Console.WriteLine($"[DNS] IP Addresses: {string.Join(", ", ips.Select(ip => ip.ToString()))}");

        // 2. Gọi HTTP Client
        using var httpClient = new HttpClient();
        using var context = GetDbContext(); // Sử dụng hàm GetDbContext() của bạn
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync(uri);
            stopwatch.Stop();

            // Ghi DeviceLog cho HTTP
            context.DeviceLogs.Add(new DeviceLog
            {
                Protocol = "HTTP",
                SourceAddress = uri.Host,
                Content = $"Đồng bộ giá. Status: {response.StatusCode}",
                Timestamp = DateTime.Now
            });
            await context.SaveChangesAsync();

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var priceUpdates = JsonSerializer.Deserialize<List<PriceUpdateDto>>(json);

                if (priceUpdates != null)
                {
                    int updateCount = 0;
                    foreach (var update in priceUpdates)
                    {
                        var item = await context.MenuItems.FindAsync(update.Id);
                        if (item != null && item.Price != update.Price)
                        {
                            item.Price = update.Price;
                            updateCount++;
                        }
                    }
                    await context.SaveChangesAsync();
                    Console.WriteLine($"[HTTP] Đã cập nhật giá cho {updateCount} món (Phản hồi: {stopwatch.ElapsedMilliseconds}ms)\n");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LỖI HTTP]: {ex.Message}");
        }
    }

    // Hàm đọc lệnh từ bàn phím để phát UDP
    static async Task HandleAdminCommandsAsync()
    {
        using var udpServer = new UdpClient();
        udpServer.EnableBroadcast = true;
        var endPoint = new IPEndPoint(IPAddress.Broadcast, 9501);

        while (true)
        {
            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input) && input.StartsWith("HET "))
            {
                string itemId = input.Substring(4);

                using var context = GetDbContext(); // Sử dụng hàm GetDbContext() của bạn
                var item = await context.MenuItems.FindAsync(itemId);
                if (item != null)
                {
                    item.IsAvailable = false;
                    await context.SaveChangesAsync();

                    byte[] bytes = Encoding.UTF8.GetBytes(itemId);
                    await udpServer.SendAsync(bytes, bytes.Length, endPoint);
                    Console.WriteLine($"[UDP BROADCAST] Đã phát thông báo hết món: {itemId}");
                }
                else
                {
                    Console.WriteLine($"[LỖI] Không tìm thấy món {itemId}");
                }
            }
        }
    }

    // DTO cho giá cập nhật
    public class PriceUpdateDto
    {
        public string Id { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

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

    // Dữ liệu trả về Quầy thu ngân
    public class OrderResponse
    {
        public string TicketId { get; set; } = string.Empty;
        public decimal FinalTotalAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

}