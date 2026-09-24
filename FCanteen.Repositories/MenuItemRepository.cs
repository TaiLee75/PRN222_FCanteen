using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Repositories;

public class MenuItemRepository : IMenuItemRepository
{
    private readonly FCanteenContext _context;

    // Sử dụng Constructor Injection
    public MenuItemRepository(FCanteenContext context)
    {
        _context = context;
    }

    public async Task<List<MenuItem>> GetAllItemsAsync()
    {
        return await _context.MenuItems.ToListAsync();
    }
}
