using SunShift.Application.Models.SmartThings;

namespace SunShift.Application.Common.Interfaces.Services;

public interface ISmartThingsClient
{
    Task<SmartThingsTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task SendCommandAsync(string accessToken, string deviceId, string command, CancellationToken cancellationToken = default);
}
