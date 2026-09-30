using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// One alert: what it is, why it fired, the rule behind it and its recent
/// history - what desktop's alert detail pane shows when an alert is
/// selected, on its own page.
/// </summary>
/// <remarks>
/// "Why it fired" is desktop's: the rule's condition names the columns that
/// matter (<see cref="AlertRuleConditions"/>), the latest alert log entry
/// for the rule holds their values (<see cref="AlertFaultParser"/>). The rule
/// and the log are best effort - a read-only token may not see rules - so
/// the alert itself still shows without them.
/// </remarks>
public sealed partial class AlertDetailViewModel : ViewModelBase
{
    /// <summary>How far back to look for this rule's log entries - desktop's search depth.</summary>
    internal const int LogDepth = 50;

    /// <summary>History rows shown.</summary>
    internal const int HistoryRows = 10;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ISelfActionTracker _selfActions;

    /// <summary>
    /// Raised after you acknowledge or unacknowledge the alert here. Beside
    /// the Alerts list on a larger screen (#88) the list stays on screen, so
    /// it changes that row itself rather than waiting to reappear.
    /// </summary>
    public event EventHandler<AlertStateChange>? AlertChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlert))]
    [NotifyPropertyChangedFor(nameof(CanAcknowledge))]
    [NotifyPropertyChangedFor(nameof(CanUnacknowledge))]
    [NotifyPropertyChangedFor(nameof(RaisedText))]
    private AlertItem? _alert;

    [ObservableProperty]
    private string _title = "Alert";

    /// <summary>The alert has cleared and LibreNMS no longer has it (a recovery notification, say).</summary>
    [ObservableProperty]
    private bool _isGone;

    public AlertDetailViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        IDialogService dialogs,
        INavigationService navigation,
        ISelfActionTracker selfActions)
    {
        _client = client;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;
        _selfActions = selfActions;
    }

    public int AlertId { get; private set; }

    /// <summary>Known before the alert loads when a notification said so, which keeps "Open device" working for a cleared alert.</summary>
    public int? DeviceId { get; private set; }

    public bool HasAlert => Alert is not null;

    public bool CanAcknowledge => Alert is { IsAcknowledged: false, State: not AlertState.Recovered };

    public bool CanUnacknowledge => Alert is { IsAcknowledged: true };

    public bool HasDevice => DeviceId is not null;

    /// <summary>Alert facts, faults, the rule and history, as Device View's grouped rows.</summary>
    public BulkObservableCollection<SectionGroup> Groups { get; } = new();

    /// <summary>
    /// The groups drawn as cards under the header: all but "Alert", whose
    /// device, severity, state and time the header card already shows.
    /// </summary>
    public IReadOnlyList<SectionGroup> CardGroups => Groups.Where(g => g.Name != AlertGroupName).ToList();

    /// <summary>"Raised 38m ago, 09:14 · #4821" - the header card's quiet line.</summary>
    public string? RaisedText => Alert is not { } alert ? null : string.Join(" · ", new[]
    {
        "Raised " + (alert.AgeText.Length > 0 ? alert.AgeText : "at an unknown time")
            + (alert.LocalTimestamp is { } at ? ", " + at.ToString("HH:mm", CultureInfo.CurrentCulture) : string.Empty),
        "#" + alert.Id.ToString(CultureInfo.InvariantCulture),
    });

    internal const string AlertGroupName = "Alert";

    public Task LoadAsync(int alertId, int? deviceId = null)
    {
        AlertId = alertId;
        DeviceId = deviceId;
        OnPropertyChanged(nameof(HasDevice));
        return RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var utc = _settings.Current.ServerTimestampsAreUtc;
        var alert = await _client.Alerts.GetAsync(AlertId);
        if (alert is null)
        {
            Alert = null;
            IsGone = true;
            Title = "Alert cleared";
            Groups.ReplaceAll([]);
            OnPropertyChanged(nameof(CardGroups));
            return;
        }

        IsGone = false;

        // Named by the Device names setting, as everywhere else; the alert
        // alone only carries the hostname.
        var device = await BestEffort(() => _client.Devices.GetAsync(alert.DeviceId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Alert = new AlertItem(alert, utc, _settings.Current.DeviceNameStyle.Resolve(device, alert.Hostname));
        DeviceId = alert.DeviceId;
        OnPropertyChanged(nameof(HasDevice));
        Title = alert.DisplayRuleName;

        var ruleTask = BestEffort(() => _client.Rules.GetAsync(alert.RuleId));
        var logTask = BestEffort(() => _client.Logs.ListAlertLogAsync(alert.DeviceId, LogDepth));
        await Task.WhenAll(ruleTask, logTask);

        var rule = ruleTask.Result;
        var log = (logTask.Result ?? []).Where(e => e.RuleId == alert.RuleId).ToList();

        _rule = rule;
        _ruleLog = log;
        ShowGroups();
    });

    private AlertRule? _rule;
    private IReadOnlyList<AlertLogEntry> _ruleLog = [];

    /// <summary>
    /// "Why it fired" shows the columns the rule tests by default (#46) -
    /// desktop keeps the rest for when the reader asks for the full row.
    /// This is that ask, for every match at once.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllFieldsText))]
    private bool _showAllFields;

    /// <summary>Whether there's anything the toggle would add.</summary>
    [ObservableProperty]
    private bool _hasMoreFields;

    public string AllFieldsText => ShowAllFields ? "Show only what the rule tests" : "Show all fields";

    partial void OnShowAllFieldsChanged(bool value) => ShowGroups();

    [RelayCommand]
    private void ToggleAllFields() => ShowAllFields = !ShowAllFields;

    private void ShowGroups()
    {
        if (Alert is not { } alert)
        {
            return;
        }

        var detail = AlertFaultParser.Parse(_ruleLog.FirstOrDefault(), AlertRuleConditions.ExtractFields(_rule));
        HasMoreFields = detail.Faults.Concat(detail.Resolved).Any(HasHiddenFields);
        var groups = BuildGroups(alert, _rule, _ruleLog, _settings.Current.ServerTimestampsAreUtc, ShowAllFields).ToList();
        if (HasMoreFields || ShowAllFields)
        {
            // The link on Why it fired's card. On the group rather than bound
            // back to this view model from inside the card list, where the
            // binding found nothing and left an empty gap (Batch 11).
            foreach (var group in groups.Where(g => g.HasFields))
            {
                group.FooterText = AllFieldsText;
                group.FooterCommand = ToggleAllFieldsCommand;
            }
        }

        Groups.ReplaceAll(groups);
        OnPropertyChanged(nameof(CardGroups));
    }

    /// <summary>Acknowledges until the alert clears, with an optional note - as the list does.</summary>
    [RelayCommand]
    private async Task AcknowledgeAsync()
    {
        if (Alert is not { } item || !CanAcknowledge)
        {
            return;
        }

        var note = await _dialogs.PromptAsync(
            "Acknowledge alert",
            $"{item.Rule} on {item.Device}. Add a note (optional):",
            "Acknowledge",
            "Note");
        if (note is null)
        {
            return;
        }

        if (await RunAsync(() => _client.Alerts.AcknowledgeAsync(item.Id, note.Trim())))
        {
            // So the next alert check doesn't notify you about your own acknowledgement.
            _selfActions.Record(item.Id, AlertChangeKind.Acknowledged);
            AlertChanged?.Invoke(this, new AlertStateChange(item.Id, Acknowledged: true, string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task UnacknowledgeAsync()
    {
        if (Alert is not { } item || !CanUnacknowledge)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Unacknowledge alert",
            $"Put {item.Rule} on {item.Device} back to active?",
            "Unacknowledge",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        if (await RunAsync(() => _client.Alerts.UnmuteAsync(item.Id)))
        {
            _selfActions.Record(item.Id, AlertChangeKind.Unacknowledged);
            AlertChanged?.Invoke(this, new AlertStateChange(item.Id, Acknowledged: false, Note: null));
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private Task OpenDeviceAsync() => DeviceId is { } id
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : Task.CompletedTask;

    internal static IEnumerable<SectionGroup> BuildGroups(AlertItem alert, AlertRule? rule, IReadOnlyList<AlertLogEntry> ruleLog, bool utc, bool showAllFields = false)
    {
        var status = StatusFor(alert.Severity);

        var facts = new List<SectionRow>
        {
            new("Device") { Value = alert.Device },
            new("Severity") { Value = alert.SeverityText, Status = status },
            new("State") { Value = alert.StateText, Status = alert.IsAcknowledged ? RowStatus.Inactive : status },
            new("Raised") { Value = alert.LocalTimestamp is { } at ? When(at) : "Unknown", Detail = alert.AgeText },
        };
        if (alert.Note is { } note)
        {
            facts.Add(new SectionRow("Note") { Subtitle = note });
        }

        yield return new SectionGroup(AlertGroupName, facts);

        // The newest log entry for the rule carries the faults for the alert showing now.
        var detail = AlertFaultParser.Parse(ruleLog.FirstOrDefault(), AlertRuleConditions.ExtractFields(rule));
        if (detail.HasFaults)
        {
            yield return new SectionGroup(
                detail.Faults.Count == 1 ? "Why it fired" : $"Why it fired · {detail.Faults.Count} matches",
                detail.Faults.Select(f => FaultRow(f, status, showAllFields)))
            {
                HasFields = true,
            };
        }

        if (detail.Resolved.Count > 0)
        {
            yield return new SectionGroup("Cleared since the last check", detail.Resolved.Select(f => FaultRow(f, RowStatus.Ok, showAllFields)));
        }

        if (rule is not null)
        {
            yield return new SectionGroup("Rule", RuleRows(rule));
        }

        var history = ruleLog.Take(HistoryRows).Select(e => new SectionRow(e.State.ToDisplayString())
        {
            Value = ServerTime.ToLocal(e.TimeLogged, utc) is { } at ? When(at) : null,
            Status = e.State switch
            {
                AlertState.Recovered or AlertState.Better => RowStatus.Ok,
                AlertState.Acknowledged => RowStatus.Inactive,
                _ => status,
            },
        }).ToList();
        if (history.Count > 0)
        {
            yield return new SectionGroup("History", history);
        }
    }

    /// <summary>
    /// When the rule's tested columns can't be picked out, how many of the
    /// measured fields show before "Show all fields".
    /// </summary>
    internal const int UntestedFieldsShown = 3;

    /// <summary>
    /// A fault as one row: what faulted ("Gi0/1 - uplink to core") and the
    /// values the rule tests. Everything else measured - or, when the tested
    /// columns can't be picked out, all but the first few - only with
    /// <paramref name="showAllFields"/>, smaller (#46).
    /// </summary>
    internal static SectionRow FaultRow(AlertFault fault, RowStatus status, bool showAllFields = false) => new(fault.Title)
    {
        Fields =
        [
            .. (showAllFields || fault.HasTriggerFields ? fault.PrimaryFields : fault.PrimaryFields.Take(UntestedFieldsShown))
                .Select(f => new SectionField(f.Name, f.Value)),
            .. (showAllFields ? fault.SecondaryFields : [])
                .Select(f => new SectionField(f.Name, f.Value, IsSecondary: true)),
        ],
        Status = status,
    };

    /// <summary>Whether <see cref="FaultRow"/> leaves anything out until "Show all fields".</summary>
    private static bool HasHiddenFields(AlertFault fault) =>
        fault.HasSecondaryFields || (!fault.HasTriggerFields && fault.PrimaryFields.Count > UntestedFieldsShown);

    private static IEnumerable<SectionRow> RuleRows(AlertRule rule)
    {
        // The builder's condition in LibreNMS's own SQL-ish form; the legacy
        // text, or the hand-written query, when there's no builder.
        var condition = rule.Extra?.OverrideQuery == true
            ? rule.Query
            : AlertRuleSqlFormatter.Format(rule.Builder) ?? rule.Rule ?? rule.Query;

        // As the mock-up: the rule's name over its condition, and notes and
        // procedure below, without lines between - one card, not a list. The
        // severity is in the header already; only "Disabled" is news here.
        yield return new SectionRow(rule.Name ?? $"Rule {rule.Id}")
        {
            Value = rule.Disabled ? "Disabled" : null,
            Status = rule.Disabled ? RowStatus.Inactive : RowStatus.None,
            Subtitle = string.IsNullOrWhiteSpace(condition) ? null : BreakCondition(condition.Trim()),
            IsCode = true,
            IsStacked = true,
            Detail = rule.Extra?.Invert == true ? "Inverted: alerts when the condition doesn't match" : null,
        };

        if (!string.IsNullOrWhiteSpace(rule.Notes))
        {
            yield return new SectionRow("Notes") { Subtitle = rule.Notes, IsStacked = true };
        }

        if (!string.IsNullOrWhiteSpace(rule.Procedure))
        {
            yield return new SectionRow("Procedure") { Subtitle = rule.Procedure, IsStacked = true };
        }
    }

    /// <summary>
    /// A rule's condition with each top-level AND / OR starting a line, as
    /// the mock-up's rule card: one run-on line was hard to read (Batch 11).
    /// Bracketed groups and quoted text ("(power|psu)") stay as they are.
    /// </summary>
    internal static string BreakCondition(string condition)
    {
        var text = new StringBuilder(condition.Length + 16);
        var depth = 0;
        char? quote = null;
        for (var i = 0; i < condition.Length; i++)
        {
            var c = condition[i];
            if (quote is not null)
            {
                if (c == quote)
                {
                    quote = null;
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && c == ' ' && StartsOperator(condition, i + 1) is { } length)
            {
                // " AND x" -> "\nAND x": the operator leads its line.
                text.Append('\n').Append(condition, i + 1, length);
                i += length;
                continue;
            }

            text.Append(c);
        }

        return text.ToString();
    }

    /// <summary>The length of an AND / OR / &amp;&amp; / || starting at <paramref name="at"/> and followed by a space.</summary>
    private static int? StartsOperator(string text, int at)
    {
        foreach (var op in (string[])["AND", "OR", "&&", "||"])
        {
            if (at + op.Length < text.Length
                && string.Compare(text, at, op, 0, op.Length, StringComparison.OrdinalIgnoreCase) == 0
                && text[at + op.Length] == ' ')
            {
                return op.Length;
            }
        }

        return null;
    }

    private static RowStatus StatusFor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => RowStatus.Critical,
        AlertSeverity.Warning => RowStatus.Warning,
        _ => RowStatus.None,
    };

    private static string When(DateTime local) => local.ToString("g", CultureInfo.CurrentCulture);

    /// <summary>Rules and the log are extras: without them the alert still shows.</summary>
    private static async Task<T?> BestEffort<T>(Func<Task<T>> load)
    {
        try
        {
            return await load();
        }
        catch (LibreNmsApiException)
        {
            return default;
        }
    }
}

/// <summary>An alert acknowledged or unacknowledged on its own page - see <see cref="AlertDetailViewModel.AlertChanged"/>.</summary>
/// <param name="Note">The acknowledgement's note, if one was given.</param>
public sealed record AlertStateChange(int AlertId, bool Acknowledged, string? Note);
