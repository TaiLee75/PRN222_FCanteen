namespace FCanteen.Services.Notifications;

public class FileNotificationService : INotificationService
{
    public async Task SendAsync(string message)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
        await File.AppendAllTextAsync("notification_logs.txt", logMessage);
    }
}
