using SunShift.Application.Models.Notifications;

namespace SunShift.Application.Common.Interfaces.Services;

/// <summary>
/// Low-level Firebase Cloud Messaging HTTP v1 client. Sends one message to one device token
/// and reports whether the token should be pruned.
/// </summary>
public interface IFcmClient
{
    Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default);
}
