namespace SunShift.Application.Common.Interfaces.Services;

public interface IScheduleProgressNotifier
{
    Task NotifyAsync(int userId, string stage, int progressPercent, CancellationToken cancellationToken = default);
    Task NotifyDeviceStatusAsync(
        int userId,
        int scheduleId,
        int workloadId,
        string deviceId,
        string status,
        string scheduleStatus,
        CancellationToken cancellationToken = default);
}
