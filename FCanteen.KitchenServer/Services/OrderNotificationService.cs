using FCanteen.Data;
using FCanteen.Data.Entities;
using FCanteen.KitchenServer.Hubs;
using FCanteen.KitchenServer.Hubs.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.KitchenServer.Services
{
    public interface IOrderNotificationService
    {
        Task PublishOrderCreatedAsync(OrderCreatedDto order);
        Task PublishOrderStatusChangedAsync(OrderStatusChangedDto statusDto);
        Task PublishMenuItemSoldOutAsync(string menuItemId, string itemName);
    }

    public class OrderNotificationService : IOrderNotificationService
    {
        private readonly IHubContext<OrderHub, IOrderClient> _hubContext;
        private readonly IDbContextFactory<FCanteenContext> _dbFactory;
        private readonly ILogger<OrderNotificationService> _logger;

        public OrderNotificationService(
            IHubContext<OrderHub, IOrderClient> hubContext,
            IDbContextFactory<FCanteenContext> dbFactory,
            ILogger<OrderNotificationService> logger)
        {
            _hubContext = hubContext;
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task PublishOrderCreatedAsync(OrderCreatedDto order)
        {
            string targetGroup = $"branch:{order.BranchCode}:kitchen";
            await _hubContext.Clients.Group(targetGroup).OrderCreated(order);

            await SaveNotificationAsync(
                eventType: "OrderCreated",
                content: $"Đơn hàng mới [{order.TicketId}] tại chi nhánh {order.BranchCode}. Tổng tiền: {order.TotalAmount:N0}đ",
                recipient: targetGroup
            );
        }

        public async Task PublishOrderStatusChangedAsync(OrderStatusChangedDto statusDto)
        {
            string customerGroup = $"order:{statusDto.TicketId}";
            string kitchenGroup = $"branch:{statusDto.BranchCode}:kitchen";
            string counterGroup = $"branch:{statusDto.BranchCode}:counter";

            // Bắn cho khách xem đơn của mình
            await _hubContext.Clients.Group(customerGroup).OrderStatusChanged(statusDto);

            // Bắn cho bếp cùng chi nhánh để đồng bộ
            await _hubContext.Clients.Group(kitchenGroup).OrderStatusChanged(statusDto);

            // Bắn cho quầy
            await _hubContext.Clients.Group(counterGroup).OrderStatusChanged(statusDto);

            await SaveNotificationAsync(
                eventType: "OrderStatusChanged",
                content: $"Đơn hàng [{statusDto.TicketId}] đổi trạng thái sang: {statusDto.NewStatusText} bởi {statusDto.ChangedBy}",
                recipient: $"{customerGroup}, {kitchenGroup}, {counterGroup}"
            );
        }

        public async Task PublishMenuItemSoldOutAsync(string menuItemId, string itemName)
        {
            await _hubContext.Clients.All.MenuItemSoldOut(menuItemId, itemName);

            await SaveNotificationAsync(
                eventType: "MenuItemSoldOut",
                content: $"Món ăn [{menuItemId}] {itemName} đã hết hàng (Sold Out)!",
                recipient: "All"
            );
        }

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
                _logger.LogError(ex, $"[SignalR Service] Lỗi khi lưu Notification: {ex.Message}");
            }
        }
    }
}
