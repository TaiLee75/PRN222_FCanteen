using FCanteen.Data.Entities;
using FCanteen.Repositories;
using FCanteen.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FCanteen.Data;

namespace FCanteen.WebApp.Controllers
{
    public class MenuItemsController : Controller
    {
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly FCanteenContext _context;

        public MenuItemsController(IMenuItemRepository menuItemRepository, FCanteenContext context)
        {
            _menuItemRepository = menuItemRepository;
            _context = context;
        }

        // YC3: Tìm kiếm, lọc, sắp xếp, 4 cách truyền dữ liệu
        public async Task<IActionResult> Index(string searchString, int? categoryId, bool? isAvailable, string sortOrder, int? pageNumber)
        {
            // ViewData
            ViewData["CurrentSearch"] = searchString;
            ViewData["NameSortParm"] = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["PriceSortParm"] = sortOrder == "price" ? "price_desc" : "price";
            
            // ViewBag
            ViewBag.CurrentSort = sortOrder;
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", categoryId);
            ViewBag.CurrentCategory = categoryId;
            ViewBag.CurrentIsAvailable = isAvailable;

            var items = _menuItemRepository.GetMenuItemsQueryable();

            // Lọc
            if (!String.IsNullOrEmpty(searchString))
            {
                items = items.Where(s => s.Name.Contains(searchString) || s.Id.Contains(searchString));
            }
            if (categoryId.HasValue)
            {
                items = items.Where(s => s.CategoryId == categoryId);
            }
            if (isAvailable.HasValue)
            {
                items = items.Where(s => s.IsAvailable == isAvailable.Value);
            }

            // Sắp xếp
            switch (sortOrder)
            {
                case "name_desc":
                    items = items.OrderByDescending(s => s.Name);
                    break;
                case "price":
                    items = items.OrderBy(s => s.Price);
                    break;
                case "price_desc":
                    items = items.OrderByDescending(s => s.Price);
                    break;
                default:
                    items = items.OrderBy(s => s.Name);
                    break;
            }

            int pageSize = 5; // Số phần tử mỗi trang
            return View(await PaginatedList<MenuItem>.CreateAsync(items.AsNoTracking(), pageNumber ?? 1, pageSize));
        }

        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();
            var item = await _menuItemRepository.GetItemByIdAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.CategoryId = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuItemViewModel model)
        {
            if (await _menuItemRepository.ExistsAsync(model.Id))
            {
                ModelState.AddModelError("Id", "Mã món đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                var entity = new MenuItem
                {
                    Id = model.Id,
                    Name = model.Name,
                    Unit = model.Unit,
                    Price = model.Price,
                    CostPrice = model.CostPrice,
                    IsAvailable = model.IsAvailable,
                    CategoryId = model.CategoryId
                };
                await _menuItemRepository.AddAsync(entity);
                
                // TempData
                TempData["SuccessMessage"] = "Thêm món ăn thành công!";
                return RedirectToAction(nameof(Index));
            }
            
            ViewBag.CategoryId = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", model.CategoryId);
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();
            var item = await _menuItemRepository.GetItemByIdAsync(id);
            if (item == null) return NotFound();

            var model = new MenuItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Unit = item.Unit,
                Price = item.Price,
                CostPrice = item.CostPrice,
                IsAvailable = item.IsAvailable,
                CategoryId = item.CategoryId
            };

            ViewBag.CategoryId = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", item.CategoryId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, MenuItemViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var entity = await _menuItemRepository.GetItemByIdAsync(id);
                if (entity == null) return NotFound();

                entity.Name = model.Name;
                entity.Unit = model.Unit;
                entity.Price = model.Price;
                entity.CostPrice = model.CostPrice;
                entity.IsAvailable = model.IsAvailable;
                entity.CategoryId = model.CategoryId;

                await _menuItemRepository.UpdateAsync(entity);
                TempData["SuccessMessage"] = "Cập nhật món ăn thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.CategoryId = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", model.CategoryId);
            return View(model);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();
            var item = await _menuItemRepository.GetItemByIdAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _menuItemRepository.DeleteAsync(id);
            TempData["SuccessMessage"] = "Xóa món ăn thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ManageIngredients(string id)
        {
            if (id == null) return NotFound();
            var item = await _context.MenuItems
                .Include(m => m.MenuItemIngredients)
                .ThenInclude(mi => mi.Ingredient)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (item == null) return NotFound();

            ViewBag.Ingredients = await _context.Ingredients.ToListAsync();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> AddIngredient(string menuItemId, int ingredientId, decimal quantity)
        {
            var item = await _context.MenuItems.Include(m => m.MenuItemIngredients).FirstOrDefaultAsync(m => m.Id == menuItemId);
            if (item != null)
            {
                // Kiểm tra xem đã có chưa
                var existing = item.MenuItemIngredients.FirstOrDefault(mi => mi.IngredientId == ingredientId);
                if (existing != null)
                {
                    existing.Quantity += quantity;
                }
                else
                {
                    item.MenuItemIngredients.Add(new MenuItemIngredient
                    {
                        MenuItemId = menuItemId,
                        IngredientId = ingredientId,
                        Quantity = quantity
                    });
                }
                
                // Tự động tính giá vốn
                decimal newCostPrice = 0;
                foreach (var mi in item.MenuItemIngredients)
                {
                    var ing = await _context.Ingredients.FindAsync(mi.IngredientId);
                    if (ing != null)
                    {
                        newCostPrice += ing.UnitPrice * mi.Quantity;
                    }
                }
                item.CostPrice = newCostPrice;
                
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã thêm nguyên liệu và cập nhật giá vốn!";
            }
            return RedirectToAction(nameof(ManageIngredients), new { id = menuItemId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveIngredient(string menuItemId, int ingredientId)
        {
            var item = await _context.MenuItems.Include(m => m.MenuItemIngredients).FirstOrDefaultAsync(m => m.Id == menuItemId);
            if (item != null)
            {
                var existing = item.MenuItemIngredients.FirstOrDefault(mi => mi.IngredientId == ingredientId);
                if (existing != null)
                {
                    item.MenuItemIngredients.Remove(existing);
                    
                    // Cập nhật giá vốn
                    decimal newCostPrice = 0;
                    foreach (var mi in item.MenuItemIngredients)
                    {
                        var ing = await _context.Ingredients.FindAsync(mi.IngredientId);
                        if (ing != null)
                        {
                            newCostPrice += ing.UnitPrice * mi.Quantity;
                        }
                    }
                    item.CostPrice = newCostPrice;
                    
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Đã xóa nguyên liệu và cập nhật giá vốn!";
                }
            }
            return RedirectToAction(nameof(ManageIngredients), new { id = menuItemId });
        }

        // YC5: API Endpoint cho POS client
        [HttpGet("api/MenuItems/Available")]
        public async Task<JsonResult> GetAvailableItems()
        {
            var items = await _menuItemRepository.GetMenuItemsQueryable()
                                .Where(m => m.IsAvailable)
                                .Select(m => new { m.Id, m.Name, m.Price, m.Unit })
                                .ToListAsync();
            return Json(items);
        }
    }
}
