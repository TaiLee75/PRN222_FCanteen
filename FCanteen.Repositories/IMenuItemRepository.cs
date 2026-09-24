using FCanteen.Data.Entities;

namespace FCanteen.Repositories;

public interface IMenuItemRepository
{
    Task<List<MenuItem>> GetAllItemsAsync();
    // Các hàm khác sẽ thêm sau nếu cần
}
