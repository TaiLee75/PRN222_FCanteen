using FCanteen.Data;
using FCanteen.Data.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly FCanteenContext _context;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(FCanteenContext context, ILogger<InventoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// YC1: GET /api/inventory — Tồn kho hiện tại toàn bộ nguyên liệu
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAllInventory()
        {
            var items = await _context.Ingredients
                .Include(i => i.Supplier)
                .AsNoTracking()
                .Select(i => new InventoryItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Unit = i.Unit,
                    Stock = i.Stock,
                    WarningThreshold = i.WarningThreshold,
                    UnitPrice = i.UnitPrice,
                    SupplierId = i.SupplierId,
                    SupplierName = i.Supplier != null ? i.Supplier.Name : null
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// YC1: GET /api/inventory/low-stock — Nguyên liệu dưới hoặc bằng ngưỡng cảnh báo
        /// </summary>
        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetLowStockInventory()
        {
            var lowStockItems = await _context.Ingredients
                .Include(i => i.Supplier)
                .AsNoTracking()
                .Where(i => i.Stock <= i.WarningThreshold)
                .Select(i => new InventoryItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Unit = i.Unit,
                    Stock = i.Stock,
                    WarningThreshold = i.WarningThreshold,
                    UnitPrice = i.UnitPrice,
                    SupplierId = i.SupplierId,
                    SupplierName = i.Supplier != null ? i.Supplier.Name : null
                })
                .ToListAsync();

            _logger.LogInformation($"[InventoryApi] Đã truy vấn low-stock: tìm thấy {lowStockItems.Count} nguyên liệu dưới ngưỡng.");
            return Ok(lowStockItems);
        }

        /// <summary>
        /// YC1: POST /api/inventory/{id}/adjust — Điều chỉnh tồn kho nguyên liệu (+ nhập thêm, - xuất bớt)
        /// </summary>
        [HttpPost("{id}/adjust")]
        public async Task<ActionResult<InventoryItemDto>> AdjustStock(int id, [FromBody] AdjustStockDto request)
        {
            var ingredient = await _context.Ingredients
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (ingredient == null)
            {
                return NotFound(new { message = $"Không tìm thấy nguyên liệu có ID = {id}" });
            }

            decimal oldStock = ingredient.Stock;
            decimal newStock = ingredient.Stock + request.QuantityChange;

            if (newStock < 0)
            {
                return BadRequest(new { message = $"Tồn kho không thể âm! Tồn hiện tại: {oldStock}, lượng muốn trừ: {request.QuantityChange}" });
            }

            ingredient.Stock = newStock;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"[InventoryApi] Đã điều chỉnh nguyên liệu [{ingredient.Name}] (ID: {id}): {oldStock} -> {newStock} (Thay đổi: {request.QuantityChange}, Lý do: {request.Reason ?? "Không có"})");

            var resultDto = new InventoryItemDto
            {
                Id = ingredient.Id,
                Name = ingredient.Name,
                Unit = ingredient.Unit,
                Stock = ingredient.Stock,
                WarningThreshold = ingredient.WarningThreshold,
                UnitPrice = ingredient.UnitPrice,
                SupplierId = ingredient.SupplierId,
                SupplierName = ingredient.Supplier?.Name
            };

            return Ok(resultDto);
        }
    }
}
