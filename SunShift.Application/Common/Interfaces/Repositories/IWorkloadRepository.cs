using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IWorkloadRepository
{
    Task<List<Workload>> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
