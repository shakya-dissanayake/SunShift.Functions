using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<List<(double Latitude, double Longitude)>> GetDistinctCoordinatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Users
            .Where(u => u.SolarProfile != null)
            .Select(u => new { u.Latitude, u.Longitude })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Latitude, r.Longitude)).ToList();
    }

    public Task<List<User>> GetAllActiveWithProfilesAsync(CancellationToken cancellationToken = default)
        => context.Users
            .Include(u => u.SolarProfile)
            .ToListAsync(cancellationToken);

    public Task<User?> GetByIdWithProfileAsync(int userId, CancellationToken cancellationToken = default)
        => context.Users
            .Include(u => u.SolarProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<List<User>> GetAllActiveWithWorkloadsAsync(CancellationToken cancellationToken = default)
        => context.Users
            .Include(u => u.Workloads)
            .ToListAsync(cancellationToken);

    public Task<User?> GetByIdWithWorkloadsAsync(int userId, CancellationToken cancellationToken = default)
        => context.Users
            .Include(u => u.Workloads)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
}
