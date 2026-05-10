using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public Task<List<User>> GetAllActiveWithProfilesAsync(CancellationToken cancellationToken = default)
        => context.Users
            .Include(u => u.SolarProfile)
            .Include(u => u.Location)
            .ToListAsync(cancellationToken);

    public Task<User?> GetByIdWithProfileAsync(int userId, CancellationToken cancellationToken = default)
        => context.Users
            .Include(u => u.SolarProfile)
            .Include(u => u.Location)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
}
