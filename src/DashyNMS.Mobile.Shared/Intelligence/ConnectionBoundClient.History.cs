using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

internal sealed partial class ConnectionBoundClient
{
    private Task<T> ReadHistoryDeviceAsync<T>(int id, Func<LibreNmsResourceApis, Task<T>> read)
    {
        if (!historyProfile || id <= 0) throw new RefusedException("History device read outside its reviewed profile");
        return read(Apis());
    }
    private sealed class HistoryLogsOnly(ConnectionBoundClient owner) : ILogsApi
    {
        private static void Check(int? id, int limit)
        {
            if (id is <= 0 || limit is not (200 or 1000 or 2000)) throw new RefusedException("History query outside its bounded depths");
        }
        public Task<IReadOnlyList<AlertLogEntry>> ListAlertLogAsync(int? deviceId, int limit = 50, CancellationToken cancellationToken = default)
        { Check(deviceId, limit); return owner.Apis().Logs.ListAlertLogAsync(deviceId, limit, cancellationToken); }
        public Task<IReadOnlyList<EventLogEntry>> ListEventLogAsync(int? deviceId, int limit = 50, CancellationToken cancellationToken = default)
        { Check(deviceId, limit); return owner.Apis().Logs.ListEventLogAsync(deviceId, limit, cancellationToken); }
        public Task<AlertLogEntry?> GetLatestForRuleAsync(int deviceId, int ruleId, int searchDepth = 50, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetLatestForRuleAsync));
    }
}
