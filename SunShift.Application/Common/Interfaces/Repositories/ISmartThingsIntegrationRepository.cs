using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface ISmartThingsIntegrationRepository
{
    Task<SmartThingsIntegration?> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<SmartThingsIntegration>> GetActiveExpiringSoonAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default);
}
