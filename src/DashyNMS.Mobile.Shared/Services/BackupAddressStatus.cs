using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopNMS.Core.Api;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Whether the app is talking to the server's backup address, and the way
/// back (#114). Core's <see cref="ServerFailover"/> moves to the backup after
/// two requests in a row can't reach the main address, and on purpose never
/// moves back on its own - so, as desktop's title-bar warning does, this
/// says so (an amber "On backup address" pill on the dashboard, and the
/// Settings › Server row) and offers "Switch back to the server address".
/// </summary>
/// <remarks>
/// If the main address still doesn't answer, two more failures move it to
/// the backup again, as on desktop. Both moves are logged by Core's
/// transport, so they're in the diagnostics too (#126).
/// </remarks>
public sealed partial class BackupAddressStatus : ObservableObject
{
    private readonly ServerFailover _failover;
    private readonly ISessionService _session;
    private readonly IDialogService? _dialogs;
    private readonly ILogger _logger;
    private readonly SynchronizationContext? _context;

    public BackupAddressStatus(ServerFailover failover, ISessionService session, IDialogService? dialogs = null, ILogger<BackupAddressStatus>? logger = null)
    {
        _failover = failover;
        _session = session;
        _dialogs = dialogs;
        _logger = logger ?? NullLogger<BackupAddressStatus>.Instance;

        // Failover changes on whichever thread made the request: the pill follows on the one that made this.
        _context = SynchronizationContext.Current;
        _failover.Changed += (_, _) =>
        {
            if (_context is null)
            {
                Raise();
            }
            else
            {
                _context.Post(_ => Raise(), null);
            }
        };
    }

    /// <summary>Switched back by hand: the page showing loads again from the server address.</summary>
    public event EventHandler? SwitchedBack;

    public bool IsOnBackup => _failover.IsOnBackup;

    /// <summary>"10.44.100.11" - the backup address as desktop shows it.</summary>
    public string BackupHost => _failover.BackupAddress is { } address
        ? Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : address
        : string.Empty;

    /// <summary>"17:35" - when it moved.</summary>
    public string SinceText => _failover.SwitchedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture) ?? string.Empty;

    private string ServerHost => _session.Connection?.WebRoot.Host ?? "The server";

    /// <summary>"Connected through the backup address 10.44.100.11 since 17:35. nms.example.net stopped answering." - desktop's words.</summary>
    public string Explanation => IsOnBackup
        ? $"Connected through the backup address {BackupHost}{(SinceText.Length > 0 ? " since " + SinceText : string.Empty)}. {ServerHost} stopped answering."
        : string.Empty;

    /// <summary>The Settings › Server row: "On the backup address 10.44.100.11 since 17:35".</summary>
    public string ServerRowText => IsOnBackup
        ? $"On the backup address {BackupHost}{(SinceText.Length > 0 ? " since " + SinceText : string.Empty)}"
        : string.Empty;

    /// <summary>The pill's tap: what's happening, and the way back.</summary>
    [RelayCommand]
    private async Task ExplainAsync()
    {
        if (!IsOnBackup)
        {
            return;
        }

        if (_dialogs is null || await _dialogs.ConfirmAsync("Backup address", Explanation, "Switch back to the server address", "Not now"))
        {
            SwitchBack();
        }
    }

    /// <summary>Desktop's "Switch back to the server address" - Core's <see cref="ServerFailover.FailBack"/>.</summary>
    [RelayCommand]
    private void SwitchBack()
    {
        if (_failover.FailBack())
        {
            _logger.LogInformation("Switched back to the server address by hand");
            Raise();
            SwitchedBack?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Raise()
    {
        OnPropertyChanged(nameof(IsOnBackup));
        OnPropertyChanged(nameof(BackupHost));
        OnPropertyChanged(nameof(SinceText));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(ServerRowText));
    }
}
