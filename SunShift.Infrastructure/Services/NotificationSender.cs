using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Application.Models.Notifications;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Services;

/// <summary>
/// Fans a push notification out to every registered device of a user, pruning any tokens
/// FCM reports as invalid so dead devices stop being targeted.
/// </summary>
public class NotificationSender(
    IDeviceTokenRepository deviceTokenRepository,
    IFcmClient fcmClient,
    ILogger<NotificationSender> logger) : INotificationSender
{
    public async Task<int> SendToUserAsync(
        int userId,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        List<DeviceToken> tokens = await deviceTokenRepository.GetByUserIdAsync(userId, cancellationToken);
        if (tokens.Count == 0)
        {
            return 0;
        }

        int delivered = 0;
        List<string> invalidTokens = [];

        foreach (DeviceToken device in tokens)
        {
            PushResult result = await fcmClient.SendAsync(device.Token, message, cancellationToken);
            switch (result)
            {
                case PushResult.Success:
                    delivered++;
                    break;
                case PushResult.TokenInvalid:
                    invalidTokens.Add(device.Token);
                    break;
            }
        }

        if (invalidTokens.Count > 0)
        {
            await deviceTokenRepository.DeleteByTokensAsync(invalidTokens, cancellationToken);
            logger.LogInformation(
                "NotificationSender: pruned {Count} invalid token(s) for user {UserId}.",
                invalidTokens.Count, userId);
        }

        return delivered;
    }
}
