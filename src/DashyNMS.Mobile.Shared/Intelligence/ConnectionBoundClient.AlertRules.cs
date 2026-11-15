using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

internal sealed partial class ConnectionBoundClient
{
    /// <summary>Only the two shared alert queries, never arbitrary filters or writes.</summary>
    private sealed class RuleAlertsOnly(ConnectionBoundClient owner) : IAlertsApi
    {
        public Task<IReadOnlyList<Alert>> ListAsync(AlertQuery? query = null, CancellationToken cancellationToken = default)
        {
            var single = query is { Id: > 0, RuleId: null, Severity: null, OrderBy: null }
                && query.States is { } all && all.Order().SequenceEqual(AlertQuery.All.States!.Order());
            var siblings = query is { RuleId: > 0, Id: null, Severity: null, OrderBy: "timestamp desc" }
                && query.States is { } open && open.Order().SequenceEqual(AlertQuery.Open.States!.Order());
            if (owner.objectProfile && !single || !ReferenceEquals(query, AlertQuery.Open) && !single && !siblings) throw new RefusedException(nameof(ListAsync));
            return owner.Apis().Alerts.ListAsync(query, cancellationToken);
        }
        public Task<AlertSource> GetSourceAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetSourceAsync));
        public Task<Alert?> GetAsync(int alertId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAsync));
        public Task AcknowledgeAsync(int alertId, string? note = null, bool untilClear = true, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(AcknowledgeAsync));
        public Task UnmuteAsync(int alertId, string? note = null, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UnmuteAsync));
    }
    private sealed class RulesOnly(ConnectionBoundClient owner) : IAlertRulesApi
    {
        public Task<IReadOnlyList<AlertRule>> ListAsync(CancellationToken cancellationToken = default) => owner.Apis().Rules.ListAsync(cancellationToken);
        public Task<AlertRule?> GetAsync(int ruleId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAsync));
        public Task CreateAsync(AlertRuleWriteRequest request, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(CreateAsync));
        public Task UpdateAsync(AlertRuleWriteRequest request, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UpdateAsync));
        public Task DeleteAsync(int ruleId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(DeleteAsync));
    }
    private sealed class TemplatesOnly(ConnectionBoundClient owner) : IAlertTemplatesApi
    {
        public Task<IReadOnlyList<AlertTemplate>> ListAsync(CancellationToken cancellationToken = default) => owner.Apis().AlertTemplates.ListAsync(cancellationToken);
        public Task<AlertTemplate?> GetAsync(int templateId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAsync));
        public Task CreateAsync(AlertTemplateWriteRequest request, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(CreateAsync));
        public Task UpdateAsync(AlertTemplateWriteRequest request, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UpdateAsync));
    }
    private sealed class LocationsOnly(ConnectionBoundClient owner) : ILocationsApi
    {
        public Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken = default) => owner.Apis().Locations.ListAsync(cancellationToken);
        public Task CreateAsync(string name, double lat, double lng, bool fixedCoordinates, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(CreateAsync));
        public Task UpdateAsync(int id, string name, double lat, double lng, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UpdateAsync));
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(DeleteAsync));
    }
    private sealed class AlertLogsOnly(ConnectionBoundClient owner) : ILogsApi
    {
        public Task<IReadOnlyList<AlertLogEntry>> ListAlertLogAsync(int? deviceId, int limit = 50, CancellationToken cancellationToken = default)
        {
            // Details read 200 for one positive device. Rule ranking reads global history
            // with the shared timeline's bounded depths (200, 1000, 2000).
            if (deviceId is <= 0 || (deviceId is not null ? limit != 200 : limit is not (200 or 1000 or 2000)))
                throw new RefusedException(nameof(ListAlertLogAsync));
            return owner.Apis().Logs.ListAlertLogAsync(deviceId, limit, cancellationToken);
        }
        public Task<AlertLogEntry?> GetLatestForRuleAsync(int deviceId, int ruleId, int searchDepth = 50, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetLatestForRuleAsync));
        public Task<IReadOnlyList<EventLogEntry>> ListEventLogAsync(int? deviceId, int limit = 50, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListEventLogAsync));
    }
}
