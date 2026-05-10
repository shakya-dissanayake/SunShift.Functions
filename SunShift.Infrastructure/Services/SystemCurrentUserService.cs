using SunShift.Application.Common.Interfaces;

namespace SunShift.Infrastructure.Services;

public class SystemCurrentUserService : ICurrentUserService
{
    public string? Email => "system";
    public string? UserId => "system";
}
