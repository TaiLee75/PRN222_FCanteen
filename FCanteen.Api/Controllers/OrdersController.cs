using FCanteen.Data;
using FCanteen.Data.Dtos;
using FCanteen.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly FCanteenContext _context;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(FCanteenContext context, ILogger<OrdersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// YC1: GET /api/orders/summary?date= — Tổng hợp doanh thu và các chỉ số trong ngày
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<OrderSummaryDto>> GetOrderSummary([FromQuery] DateTime? date)
        {
            // Mặc định lấy ngày hôm qua nếu không truyền tham số
            var targetDate = (date ?? DateTime.Today.AddDays(-1)).Date;
            var nextDate = targetDate.AddDays(1);

            var orders = await _context.OrderTickets
                .Where(o => o.CreatedAt >= targetDate && o.CreatedAt < nextDate)
                .Include(o => o.TicketLines)
                    .ThenInclude(tl => tl.MenuItem)
                .AsNoTracking()
                .ToListAsync();

            int totalOrders = orders.Count;
            int cancelledOrdersCount = orders.Count(o => o.Status == (int)OrderStatus.Cancelled);
            decimal cancellationRate = totalOrders > 0
                ? Math.Round(((decimal)cancelledOrdersCount / totalOrders) * 100, 2)
                : 0;

            // Doanh thu chỉ tính trên các đơn không bị huỷ
            var validOrders = orders.Where(o => o.Status != (int)OrderStatus.Cancelled).ToList();
            decimal totalRevenue = validOrders.Sum(o => o.TotalAmount);

            // Tìm món bán chạy nhất
            var topSelling = validOrders
                .SelectMany(o => o.TicketLines)
                .GroupBy(tl => new { tl.MenuItemId, ItemName = tl.MenuItem != null ? tl.MenuItem.Name : tl.MenuItemId })
                .Select(g => new
                {
                    Name = g.Key.ItemName,
                    TotalQuantity = g.Sum(tl => tl.Quantity)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .FirstOrDefault();

            var summary = new OrderSummaryDto
            {
                Date = targetDate,
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                CancelledOrdersCount = cancelledOrdersCount,
                CancellationRate = cancellationRate,
                TopSellingItem = topSelling != null ? topSelling.Name : "Không có",
                TopSellingQuantity = topSelling != null ? topSelling.TotalQuantity : 0
            };

            _logger.LogInformation($"[OrdersApi] Tổng hợp ngày {targetDate:yyyy-MM-dd}: Doanh thu {totalRevenue:N0}đ, {totalOrders} đơn, huỷ {cancelledOrdersCount} ({cancellationRate}%), Top: {summary.TopSellingItem} ({summary.TopSellingQuantity})");

            return Ok(summary);
        }
    }
}
