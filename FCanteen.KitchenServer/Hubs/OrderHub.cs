using FCanteen.Data;
using FCanteen.Data.Entities;
using FCanteen.KitchenServer.Hubs.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace FCanteen.KitchenServer.Hubs
{
    /// <summary>
    /// OrderHub kế thừa Hub<IOrderClient> với interface strongly-typed.
    /// Đáp ứng YC1 (Quản lý kết nối, lưu Notification, không dùng chuỗi ma thuật)
    /// Đáp ứng YC3 (Phân luồng thông tin theo Groups: branch:{branchCode}:kitchen, branch:{branchCode}:counter, order:{orderCode})
    /// </summary>
    public class OrderHub : Hub<IOrderClient>
    {
        // Sử dụng ConcurrentDictionary lưu ConnectionId để đếm chính xác 100%, không bị lệch hay nhảy số
        private static readonly ConcurrentDictionary<string, byte> _activeConnections = new();
        private readonly IDbContextFactory<FCanteenContext> _dbFactory;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(IDbContextFactory<FCanteenContext> dbFactory, ILogger<OrderHub> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public static int CurrentConnectionCount => _activeConnections.Count;

        public override async Task OnConnectedAsync()
        {
            _activeConnections.TryAdd(Context.ConnectionId, 0);
            int count = _activeConnections.Count;
            _logger.LogInformation($"[SignalR] Client kết nối: {Context.ConnectionId}. Tổng kết nối: {count}");

            // Lưu sự kiện vào bảng Notification
            await SaveNotificationAsync(
                eventType: "UpdateConnectionCount",
                content: $"Thiết bị mới kết nối (Id: {Context.ConnectionId}). Tổng số thiết bị online: {count}",
                recipient: "All"
            );

            // Bắn số kết nối mới cho toàn bộ client
            await Clients.All.UpdateConnectionCount(count);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _activeConnections.TryRemove(Context.ConnectionId, out _);
            int count = _activeConnections.Count;

            _logger.LogInformation($"[SignalR] Client ngắt kết nối: {Context.ConnectionId}. Tổng kết nối: {count}");

            // Lưu sự kiện vào bảng Notification
            await SaveNotificationAsync(
                eventType: "UpdateConnectionCount",
                content: $"Thiết bị đã ngắt kết nối (Id: {Context.ConnectionId}). Tổng số thiết bị online: {count}",
                recipient: "All"
            );

            // Bắn số kết nối mới cho toàn bộ client
            await Clients.All.UpdateConnectionCount(count);

            await base.OnDisconnectedAsync(exception);
        }

        #region YC3: Quản lý SignalR Groups theo cơ sở và vai trò

        /// <summary>
        /// Nhân viên Bếp gia nhập nhóm của cơ sở mình: ví dụ branch:DN:kitchen, branch:HL:kitchen
        /// </summary>
        public async Task JoinBranchKitchen(string branchCode)
        {
            string groupName = $"branch:{branchCode}:kitchen";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] Connection {Context.ConnectionId} đã vào nhóm: {groupName}");
        }

        public async Task LeaveBranchKitchen(string branchCode)
        {
            string groupName = $"branch:{branchCode}:kitchen";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] Connection {Context.ConnectionId} đã rời nhóm: {groupName}");
        }

        /// <summary>
        /// Nhân viên Quầy/Thu ngân gia nhập nhóm của cơ sở mình: ví dụ branch:DN:counter
        /// </summary>
        public async Task JoinBranchCounter(string branchCode)
        {
            string groupName = $"branch:{branchCode}:counter";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] Connection {Context.ConnectionId} đã vào nhóm: {groupName}");
        }

        public async Task LeaveBranchCounter(string branchCode)
        {
            string groupName = $"branch:{branchCode}:counter";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Khách hàng tra cứu gia nhập nhóm theo dõi chính đơn hàng của mình: ví dụ order:12345
        /// </summary>
        public async Task JoinOrderTrack(string orderCode)
        {
            string groupName = $"order:{orderCode}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] Khách {Context.ConnectionId} đang theo dõi đơn: {groupName}");
        }

        public async Task LeaveOrderTrack(string orderCode)
        {
            string groupName = $"order:{orderCode}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        #endregion

        #region Phát sự kiện nghiệp vụ

        /// <summary>
        /// Khi Quầy tạo đơn mới -> chỉ phát cho Bếp của chi nhánh đó
        /// </summary>
        public async Task SendOrderCreated(OrderCreatedDto order)
        {
            string targetGroup = $"branch:{order.BranchCode}:kitchen";
            await Clients.Group(targetGroup).OrderCreated(order);

            await SaveNotificationAsync(
                eventType: "OrderCreated",
                content: $"Đơn hàng mới [{order.TicketId}] tại chi nhánh {order.BranchCode}. Tổng tiền: {order.TotalAmount:N0}đ",
                recipient: targetGroup
            );
        }

        /// <summary>
        /// Khi Bếp đổi trạng thái đơn -> Quầy nhận (khi sẵn sàng), Khách nhận (cho chính đơn đó)
        /// </summary>
        public async Task SendOrderStatusChanged(OrderStatusChangedDto statusDto)
        {
            string customerGroup = $"order:{statusDto.TicketId}";
            string kitchenGroup = $"branch:{statusDto.BranchCode}:kitchen";
            string counterGroup = $"branch:{statusDto.BranchCode}:counter";

            // Bắn cho khách xem đơn của mình
            await Clients.Group(customerGroup).OrderStatusChanged(statusDto);

            // Bắn cho bếp cùng chi nhánh (để đồng bộ nếu mở nhiều tab)
            await Clients.Group(kitchenGroup).OrderStatusChanged(statusDto);

            // Nếu đơn Sẵn sàng (Ready = 3) hoặc hoàn thành -> Bắn cho Quầy biết để gọi khách lấy đồ
            await Clients.Group(counterGroup).OrderStatusChanged(statusDto);

            await SaveNotificationAsync(
                eventType: "OrderStatusChanged",
                content: $"Đơn hàng [{statusDto.TicketId}] đổi trạng thái sang: {statusDto.NewStatusText} bởi {statusDto.ChangedBy}",
                recipient: $"{customerGroup}, {kitchenGroup}, {counterGroup}"
            );
        }

        /// <summary>
        /// Khi món ăn hết hàng -> Thông báo cho toàn bộ hệ thống
        /// </summary>
        public async Task SendMenuItemSoldOut(string menuItemId, string itemName)
        {
            await Clients.All.MenuItemSoldOut(menuItemId, itemName);

            await SaveNotificationAsync(
                eventType: "MenuItemSoldOut",
                content: $"Món ăn [{menuItemId}] {itemName} đã hết hàng (Sold Out)!",
                recipient: "All"
            );
        }

        #endregion

        #region Helper lưu Notification xuống Database

        private async Task SaveNotificationAsync(string eventType, string content, string recipient)
        {
            try
            {
                using var context = _dbFactory.CreateDbContext();
                var notif = new Notification
                {
                    EventType = eventType,
                    Content = content,
                    Recipient = recipient,
                    CreatedAt = DateTime.Now
                };
                context.Notifications.Add(notif);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Lỗi khi lưu Notification: {ex.Message}");
            }
        }

        #endregion
    }
}
