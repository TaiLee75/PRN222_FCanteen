using System.ComponentModel.DataAnnotations;

namespace FCanteen.WebApp.Validations
{
    public class PriceGreaterThanCostAttribute : ValidationAttribute
    {
        private readonly double _percentage;

        public PriceGreaterThanCostAttribute(double percentage = 0.2) // default 20%
        {
            _percentage = percentage;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var price = value as decimal?;
            
            // Lấy property CostPrice từ object hiện tại
            var costPriceProperty = validationContext.ObjectType.GetProperty("CostPrice");
            
            if (costPriceProperty == null)
                return new ValidationResult("Không tìm thấy thuộc tính CostPrice.");

            var costPriceValue = costPriceProperty.GetValue(validationContext.ObjectInstance) as decimal?;

            if (price.HasValue && costPriceValue.HasValue)
            {
                var minPrice = costPriceValue.Value * (decimal)(1 + _percentage);
                if (price.Value < minPrice)
                {
                    return new ValidationResult(ErrorMessage ?? $"Giá bán phải lớn hơn giá vốn ít nhất {_percentage * 100}%.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
