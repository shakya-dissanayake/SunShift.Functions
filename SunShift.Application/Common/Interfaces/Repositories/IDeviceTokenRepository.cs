using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IDeviceTokenRepository
{
    /// <summary>Returns all active device tokens for a user (every device gets the notification).</summary>
    Task<List<DeviceToken>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes the given tokens (reported invalid by FCM). No-op for an empty list.</summary>
    Task DeleteByTokensAsync(IEnumerable<string> tokens, CancellationToken cancellationToken = default);
}
