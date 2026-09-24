namespace FCanteen.Services.Notifications;

public class EmailNotificationService : INotificationService
{
    public Task SendAsync(string message)
    {
        Console.WriteLine($"[SENDING EMAIL...] Đang gửi mail với nội dung: {message}");
        return Task.CompletedTask;
    }
}
