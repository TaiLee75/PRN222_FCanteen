using FCanteen.KitchenServer.Hubs.Dtos;

namespace FCanteen.KitchenServer.Hubs
{
    /// <summary>
    /// Interface strongly-typed định nghĩa các sự kiện máy chủ phát tới Client.
    /// Đáp ứng YC1: Interface tối thiểu có OrderCreated, OrderStatusChanged, MenuItemSoldOut, UpdateConnectionCount.
    /// </summary>
    public interface IOrderClient
    {
        Task OrderCreated(OrderCreatedDto order);
        Task OrderStatusChanged(OrderStatusChangedDto statusDto);
        Task MenuItemSoldOut(string menuItemId, string itemName);
        Task UpdateConnectionCount(int count);
        // Lab 08: Cảnh báo nguyên liệu dưới ngưỡng từ Worker Service
        Task LowStockAlert(FCanteen.Data.Dtos.StockAlertDto alert);
    }
}
