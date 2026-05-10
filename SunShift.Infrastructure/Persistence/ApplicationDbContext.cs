using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SunShift.Application.Common.Interfaces;
using SunShift.Domain.Common;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence.Configurations;

namespace SunShift.Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService) : DbContext(options)
{
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<WeatherData> WeatherData => Set<WeatherData>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SolarProfile> SolarProfiles => Set<SolarProfile>();
    public DbSet<Forecast> Forecasts => Set<Forecast>();
    public DbSet<ForecastInterval> ForecastIntervals => Set<ForecastInterval>();
    public DbSet<Workload> Workloads => Set<Workload>();
    public DbSet<Schedule> Schedules => Set<Schedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        _ = modelBuilder.ApplyConfiguration(new LocationConfiguration());
        _ = modelBuilder.ApplyConfiguration(new WeatherDataConfiguration());
        _ = modelBuilder.ApplyConfiguration(new UserConfiguration());
        _ = modelBuilder.ApplyConfiguration(new SolarProfileConfiguration());
        _ = modelBuilder.ApplyConfiguration(new ForecastConfiguration());
        _ = modelBuilder.ApplyConfiguration(new ForecastIntervalConfiguration());
        _ = modelBuilder.ApplyConfiguration(new WorkloadConfiguration());
        _ = modelBuilder.ApplyConfiguration(new ScheduleConfiguration());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        DateTime now = DateTime.Now;  // SL local time (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)
        string? currentUser = _currentUserService.UserId ?? _currentUserService.Email;

        foreach (EntityEntry<BaseAuditableEntity> entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = now;
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.IsDeleted = false;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedDate = now;
                    entry.Entity.ModifiedBy = currentUser;
                    break;
            }
        }
    }
}
