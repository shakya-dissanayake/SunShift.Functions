using SunShift.Application.Models.Notifications;

namespace SunShift.Application.Common.Interfaces.Services;

/// <summary>
/// Fans a push notification out to every registered device of a user, pruning any tokens
/// FCM reports as invalid. Returns the number of devices the message reached.
/// </summary>
public interface INotificationSender
{
    Task<int> SendToUserAsync(int userId, PushMessage message, CancellationToken cancellationToken = default);
}
