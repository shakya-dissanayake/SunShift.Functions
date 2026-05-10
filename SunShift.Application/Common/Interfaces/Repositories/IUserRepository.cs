using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IUserRepository
{
    Task<List<User>> GetAllActiveWithProfilesAsync(CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<User>> GetAllActiveWithWorkloadsAsync(CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithWorkloadsAsync(int userId, CancellationToken cancellationToken = default);
}
