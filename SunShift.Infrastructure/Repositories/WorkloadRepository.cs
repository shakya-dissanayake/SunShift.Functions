using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class WorkloadRepository(ApplicationDbContext context) : IWorkloadRepository
{
    public Task<List<Workload>> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => context.Workloads
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Priority)
            .ToListAsync(cancellationToken);
}
