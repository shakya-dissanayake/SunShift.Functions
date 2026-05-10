using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class ForecastRepository(ApplicationDbContext context) : IForecastRepository
{
    public Task<Forecast?> GetByUserAndDateAsync(
        int userId,
        DateTime targetDateSl,
        CancellationToken cancellationToken = default)
        => context.Forecasts
            .Include(f => f.Intervals)
            .FirstOrDefaultAsync(
                f => f.UserId == userId && f.TargetDate == targetDateSl.Date,
                cancellationToken);

    public async Task UpsertAsync(
        int userId,
        DateTime targetDateSl,
        double totalKwh,
        IEnumerable<ForecastInterval> intervals,
        CancellationToken cancellationToken = default)
    {
        DateTime dateOnly = targetDateSl.Date;

        // Use IgnoreQueryFilters to find soft-deleted rows too
        Forecast? existing = await context.Forecasts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.TargetDate == dateOnly, cancellationToken);

        if (existing is not null)
        {
            // Restore if soft-deleted
            existing.IsDeleted = false;
            existing.TotalPredictedKwh = totalKwh;

            // Bulk-delete previous intervals (including soft-deleted) then re-add
            await context.ForecastIntervals
                .IgnoreQueryFilters()
                .Where(fi => fi.ForecastId == existing.Id)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (ForecastInterval interval in intervals)
            {
                interval.ForecastId = existing.Id;
                context.ForecastIntervals.Add(interval);
            }
        }
        else
        {
            Forecast forecast = new()
            {
                UserId = userId,
                TargetDate = dateOnly,
                TotalPredictedKwh = totalKwh,
                Intervals = intervals.ToList()
            };
            context.Forecasts.Add(forecast);
        }
    }
}
