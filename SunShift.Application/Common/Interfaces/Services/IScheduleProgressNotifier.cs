namespace SunShift.Application.Common.Interfaces.Services;

public interface IScheduleProgressNotifier
{
    Task NotifyAsync(int userId, string stage, int progressPercent, CancellationToken cancellationToken = default);
}
