namespace FCanteen.KitchenServer.Hubs.Dtos
{
    public class TicketLineDto
    {
        public string MenuItemId { get; set; } = string.Empty;
        public string MenuItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class OrderCreatedDto
    {
        public string TicketId { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty; // Ví dụ: DN, HL
        public string PosName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Status { get; set; } // 1 = Pending
        public List<TicketLineDto> Lines { get; set; } = new();
    }

    public class OrderStatusChangedDto
    {
        public string TicketId { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public int OldStatus { get; set; }
        public int NewStatus { get; set; }
        public string NewStatusText { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
}
