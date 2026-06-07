using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class SmartThingsIntegrationRepository(ApplicationDbContext context) : ISmartThingsIntegrationRepository
{
    public Task<SmartThingsIntegration?> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => context.SmartThingsIntegrations
            .FirstOrDefaultAsync(x => x.UserId == userId
                                   && x.Status == SmartThingsIntegrationStatus.Active,
                                 cancellationToken);

    public Task<List<SmartThingsIntegration>> GetActiveExpiringSoonAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default)
        => context.SmartThingsIntegrations
            .Where(x => x.Status == SmartThingsIntegrationStatus.Active
                     && x.AccessTokenExpiresAtUtc != null
                     && x.AccessTokenExpiresAtUtc < thresholdUtc)
            .ToListAsync(cancellationToken);
}
