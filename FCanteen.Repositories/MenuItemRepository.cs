using FCanteen.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Xml.Serialization;

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
        return await _context.MenuItems.Include(m => m.Category).ToListAsync();
    }

    public IQueryable<MenuItem> GetMenuItemsQueryable()
    {
        return _context.MenuItems.Include(m => m.Category).AsQueryable();
    } 
 
    public async Task<MenuItem?> GetItemByIdAsync(string id)
    {
        return await _context.MenuItems.Include(m => m.Category).FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task AddAsync(MenuItem item)
    {
        await _context.MenuItems.AddAsync(item);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(MenuItem item)
    {
        _context.MenuItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item != null)
        {
            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _context.MenuItems.AnyAsync(m => m.Id == id);
    }
}
