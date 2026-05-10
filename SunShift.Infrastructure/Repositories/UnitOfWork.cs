using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
