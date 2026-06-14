using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class DeviceTokenRepository(ApplicationDbContext context) : IDeviceTokenRepository
{
    public Task<List<DeviceToken>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => context.DeviceTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task DeleteByTokensAsync(IEnumerable<string> tokens, CancellationToken cancellationToken = default)
    {
        List<string> list = tokens as List<string> ?? tokens.ToList();
        if (list.Count == 0)
        {
            return;
        }

        _ = await context.DeviceTokens
            .IgnoreQueryFilters()
            .Where(x => list.Contains(x.Token))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
