using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;

namespace DashyNMS.Mobile.Unimus;

/// <summary>
/// One config backup, monospaced and numbered, with a search that keeps
/// only the matching lines (and their numbers) - finding an interface or a
/// route in a long config is most of what reading one on a phone is for.
/// </summary>
public sealed partial class UnimusBackupViewModel : ViewModelBase
{
    private readonly IUnimusApi _unimus;
    private readonly IShareService _share;
    private IReadOnlyList<UnimusLine> _all = [];
    private string? _content;
    private UnimusBackupRequest? _request;
    private int _generation;
    private byte[]? _rawBytes;
    private bool _loaded;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShareStatus))]
    private string? _shareStatus;

    public bool HasShareStatus => !string.IsNullOrWhiteSpace(ShareStatus);

    public UnimusBackupViewModel(IUnimusApi unimus, IShareService share)
    {
        _unimus = unimus;
        _share = share;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy)) ShareCommand.NotifyCanExecuteChanged();
        };
    }

    public BulkObservableCollection<UnimusLine> Lines { get; } = new();

    public string Title => _request?.DeviceName ?? "Backup";

    public string Subtitle => _request is null ? string.Empty : $"Backup {_request.Backup.Id} · Unimus device {_request.UnimusDeviceId}";

    public string VersionTime => _request?.Backup.ValidSinceUtc is { } time
        ? time.ToLocalTime().ToString("d MMM yyyy HH:mm:ss zzz", System.Globalization.CultureInfo.CurrentCulture)
        : "Backup time unavailable";

    /// <summary>"12 of 340 lines" while searching; "340 lines" otherwise.</summary>
    public string CountText => string.IsNullOrWhiteSpace(SearchText)
        ? $"{_all.Count} {(_all.Count == 1 ? "line" : "lines")}"
        : $"{Lines.Count} of {_all.Count} lines";

    public bool IsEmpty => _loaded && !IsBusy && Lines.Count == 0 && !HasError;

    public string EmptyText => _content is null && _request is not null && !_request.Backup.IsText
        ? "This backup is stored as binary. Only Unimus can show it."
        : _content?.Length == 0 ? "This backup contains no configuration text." : "No lines match.";

    public bool CanShare => !IsBusy && _content is not null;

    public async Task LoadAsync(UnimusBackupRequest request, bool fetchFresh = false)
    {
        var generation = ++_generation;
        _request = request;
        _content = null;
        _rawBytes = null;
        _loaded = false;
        _all = [];
        Lines.ReplaceAll([]);
        ErrorMessage = null;
        ShareStatus = null;
        ShareCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(VersionTime));

        IsBusy = true;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        try
        {
            // The list usually carries the content; when it doesn't, Unimus
            // has no single-backup endpoint, so it's found on its page.
            var backup = request.Backup;
            if (backup.IsText && (fetchFresh || backup.Bytes is null))
            {
                var page = await _unimus.GetBackupsAsync(request.UnimusDeviceId, 0, UnimusBackupsViewModel.PageSize);
                backup = page.Backups.FirstOrDefault(b => b.Id == request.Backup.Id)
                    ?? throw new InvalidOperationException("Backup unavailable");
            }
            var content = backup.Content;
            byte[]? raw = backup.Bytes == string.Empty ? [] : backup.RawBytes;
            if (raw is { Length: 0 } && request.Backup.IsText) content = string.Empty;
            if (content is null && request.Backup.IsText)
            {
                throw new InvalidOperationException("Backup unavailable");
            }
            if (generation != _generation) return;
            _content = content;
            _rawBytes = raw;
            _loaded = true;
            _all = string.IsNullOrEmpty(content) ? [] : UnimusLine.FromContent(content);
            ApplySearch();
        }
        catch (Exception) when (generation != _generation) { }
        catch (Exception ex)
        {
            // Do not forward configuration-bearing exceptions to diagnostics.
            ErrorMessage = ex is UnimusApiException apiError
                ? apiError.ToUserMessage()
                : "Couldn't load this backup. Retry or return to backup history.";
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(EmptyText));
                OnPropertyChanged(nameof(CanShare));
                ShareCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand]
    private Task RetryAsync() => IsBusy || _request is null ? Task.CompletedTask : LoadAsync(_request, fetchFresh: true);

    partial void OnSearchTextChanged(string? value) => WhenTypingPauses(ApplySearch);

    private void ApplySearch()
    {
        var search = SearchText?.Trim();
        Lines.ReplaceAll(string.IsNullOrEmpty(search)
            ? _all
            : _all.Where(l => l.Text.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList());
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>The whole backup as a file, named for the device and the backup's time, through the share sheet.</summary>
    [RelayCommand(CanExecute = nameof(CanShare))]
    private async Task ShareAsync()
    {
        if (!CanShare || _request is null)
        {
            return;
        }

        ShareStatus = null;
        var name = UnimusExport.FileNameFor(_request.DeviceName, _request.Backup.ValidSinceUtc?.ToLocalTime(), ".cfg");
        try
        {
            if (_rawBytes is { } bytes)
                await _share.ShareBytesFileAsync(name, bytes, "text/plain", "Share backup");
        }
        catch (Exception)
        {
            ShareStatus = "Couldn't share this backup. Try Share again.";
        }
    }
}
