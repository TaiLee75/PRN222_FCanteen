using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.KitchenServer
{
    // Dữ liệu nhận từ Quầy thu ngân
    public class OrderRequest
    {
        public string PosName { get; set; } = string.Empty;
        public List<OrderLineRequest> Lines { get; set; } = new();
    }

    public class OrderLineRequest
    {
        public string MenuItemId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    // Dữ liệu trả về Quầy thu ngân
    public class OrderResponse
    {
        public string TicketId { get; set; } = string.Empty;
        public decimal FinalTotalAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
