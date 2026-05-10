using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunShift.Application.Common.Interfaces;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Common.Constants;
using SunShift.Infrastructure.Persistence;
using SunShift.Infrastructure.Repositories;
using SunShift.Infrastructure.Services;

namespace SunShift.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(
                configuration[ConfigurationKeys.DatabaseConnection],
                sqlServerOptions => sqlServerOptions
                    .EnableRetryOnFailure(3)
                    .CommandTimeout(30));
        });

        _ = services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
        _ = services.AddScoped<IUnitOfWork, UnitOfWork>();
        _ = services.AddScoped<ILocationRepository, LocationRepository>();
        _ = services.AddScoped<IWeatherDataRepository, WeatherDataRepository>();
        _ = services.AddScoped<IUserRepository, UserRepository>();
        _ = services.AddScoped<IForecastRepository, ForecastRepository>();
        _ = services.AddScoped<IWorkloadRepository, WorkloadRepository>();
        _ = services.AddScoped<IScheduleRepository, ScheduleRepository>();
    }
}
