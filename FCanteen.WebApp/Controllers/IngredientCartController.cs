using FCanteen.Data;
using FCanteen.WebApp.Extensions;
using FCanteen.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.WebApp.Controllers
{
    public class IngredientCartController : Controller
    {
        private readonly FCanteenContext _context;
        private const string CART_KEY = "IngredientCart";

        public IngredientCartController(FCanteenContext context)
        {
            _context = context;
        }

        private List<IngredientCartItem> GetCart()
        {
            return HttpContext.Session.Get<List<IngredientCartItem>>(CART_KEY) ?? new List<IngredientCartItem>();
        }

        private void SaveCart(List<IngredientCartItem> cart)
        {
            HttpContext.Session.Set(CART_KEY, cart);
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Ingredients = await _context.Ingredients.ToListAsync();
            return View(GetCart());
        }

        [HttpPost]
        public async Task<IActionResult> Add(int ingredientId, decimal quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(i => i.IngredientId == ingredientId);

            if (item != null)
            {
                item.Quantity += quantity;
            }
            else
            {
                var ingredient = await _context.Ingredients.FindAsync(ingredientId);
                if (ingredient != null)
                {
                    cart.Add(new IngredientCartItem
                    {
                        IngredientId = ingredient.Id,
                        Name = ingredient.Name,
                        Unit = ingredient.Unit,
                        Quantity = quantity
                    });
                }
            }

            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Update(int ingredientId, decimal quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(i => i.IngredientId == ingredientId);
            if (item != null)
            {
                if (quantity <= 0) cart.Remove(item);
                else item.Quantity = quantity;
                SaveCart(cart);
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int id)
        {
            var cart = GetCart();
            cart.RemoveAll(i => i.IngredientId == id);
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart();
            if (cart.Any())
            {
                foreach (var item in cart)
                {
                    var ingredient = await _context.Ingredients.FindAsync(item.IngredientId);
                    if (ingredient != null)
                    {
                        ingredient.Stock += item.Quantity; // Nhập kho
                    }
                }
                await _context.SaveChangesAsync();
                
                HttpContext.Session.Remove(CART_KEY);
                TempData["SuccessMessage"] = "Nhập nguyên liệu thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
