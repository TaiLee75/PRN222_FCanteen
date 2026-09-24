using FCanteen.Data.Entities;

namespace FCanteen.Services;

// 4. Ambient Context: Lưu trữ nhân viên hiện tại của luồng thực thi
public static class StaffContext
{
    private static readonly AsyncLocal<Staff?> _currentStaff = new AsyncLocal<Staff?>();

    public static Staff? Current
    {
        get => _currentStaff.Value;
        set => _currentStaff.Value = value;
    }
}
