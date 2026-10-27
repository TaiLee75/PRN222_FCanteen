namespace FCanteen.Data.Dtos
{
    public class StockAlertDto
    {
        public int Id { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal WarningThreshold { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime AlertTime { get; set; } = DateTime.Now;
        public string? Note { get; set; }
    }

    public class InventoryItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Stock { get; set; }
        public decimal WarningThreshold { get; set; }
        public decimal UnitPrice { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public bool IsLowStock => Stock <= WarningThreshold;
    }

    public class AdjustStockDto
    {
        // Số lượng điều chỉnh (+ tăng thêm, - giảm bớt)
        public decimal QuantityChange { get; set; }
        public string? Reason { get; set; }
    }

    public class OrderSummaryDto
    {
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int CancelledOrdersCount { get; set; }
        public decimal CancellationRate { get; set; }
        public string TopSellingItem { get; set; } = string.Empty;
        public int TopSellingQuantity { get; set; }
    }
}
