using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FCanteen.Data;
using FCanteen.Data.Entities;
using FCanteen.KitchenServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        TcpListener server = null;
        try
        {
            // Lắng nghe cổng 9500
            int port = 9500;
            server = new TcpListener(IPAddress.Any, port);
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
}