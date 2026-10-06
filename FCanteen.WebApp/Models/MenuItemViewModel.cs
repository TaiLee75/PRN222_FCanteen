using FCanteen.WebApp.Validations;
using System.ComponentModel.DataAnnotations;

namespace FCanteen.WebApp.Models
{
    public class MenuItemViewModel
    {
        [Required(ErrorMessage = "Mã món là bắt buộc.")]
        [RegularExpression(@"^MON-\d{4}$", ErrorMessage = "Mã món phải theo định dạng MON-xxxx (ví dụ: MON-0001).")]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên món là bắt buộc.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên món phải từ 3 đến 100 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đơn vị tính là bắt buộc.")]
        public string Unit { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá vốn là bắt buộc.")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá vốn phải lớn hơn hoặc bằng 0.")]
        public decimal CostPrice { get; set; }

        [Required(ErrorMessage = "Giá bán là bắt buộc.")]
        [Range(1000, 1000000, ErrorMessage = "Giá bán phải nằm trong khoảng từ 1,000 đến 1,000,000.")]
        [PriceGreaterThanCost(0.2, ErrorMessage = "Giá bán phải lớn hơn giá vốn ít nhất 20%.")]
        public decimal Price { get; set; }

        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Nhóm món")]
        public int? CategoryId { get; set; }
    }
}
