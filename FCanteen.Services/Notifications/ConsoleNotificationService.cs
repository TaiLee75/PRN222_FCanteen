namespace FCanteen.Services.Notifications;

public class ConsoleNotificationService : INotificationService
{
    public Task SendAsync(string message)
    {
        Console.WriteLine($"[CONSOLE THONG BAO]: {message}");
        return Task.CompletedTask;
    }
}
