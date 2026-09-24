namespace FCanteen.Services.Notifications;

public interface INotificationService
{
    Task SendAsync(string message);
}
