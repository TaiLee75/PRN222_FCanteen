using FCanteen.Data.Entities;

namespace FCanteen.Repositories;

public interface IMenuItemRepository
{
    Task<List<MenuItem>> GetAllItemsAsync();
    IQueryable<MenuItem> GetMenuItemsQueryable();
    Task<MenuItem?> GetItemByIdAsync(string id);
    Task AddAsync(MenuItem item);
    Task UpdateAsync(MenuItem item);
    Task DeleteAsync(string id);
    Task<bool> ExistsAsync(string id);
}
