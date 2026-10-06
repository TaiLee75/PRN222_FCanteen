namespace FCanteen.WebApp.Models
{
    public class IngredientCartItem
    {
        public int IngredientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
