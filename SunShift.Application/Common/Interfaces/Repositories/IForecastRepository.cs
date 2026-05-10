using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IForecastRepository
{
    /// <summary>
    /// Returns the forecast (with intervals) for the given user and Sri Lanka local date, or null if not found.
    /// </summary>
    Task<Forecast?> GetByUserAndDateAsync(int userId, DateTime targetDateSl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a forecast for a user + SL date.  If a row already exists, its TotalPredictedKwh is updated
    /// and all previous ForecastIntervals are replaced; the original CreatedDate is preserved.
    /// If no row exists, a new Forecast is inserted.
    /// The caller is responsible for calling SaveChangesAsync after this method returns.
    /// </summary>
    Task UpsertAsync(int userId, DateTime targetDateSl, double totalKwh, IEnumerable<ForecastInterval> intervals, CancellationToken cancellationToken = default);
}
