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

    [ObservableProperty]
    private string? _searchText;

    public UnimusBackupViewModel(IUnimusApi unimus, IShareService share)
    {
        _unimus = unimus;
        _share = share;
    }

    public BulkObservableCollection<UnimusLine> Lines { get; } = new();

    public string Title => _request is null ? "Backup" : UnimusText.Date(_request.Backup.ValidSinceUtc);

    public string Subtitle => _request?.DeviceName ?? string.Empty;

    /// <summary>"12 of 340 lines" while searching; "340 lines" otherwise.</summary>
    public string CountText => string.IsNullOrWhiteSpace(SearchText)
        ? $"{_all.Count} {(_all.Count == 1 ? "line" : "lines")}"
        : $"{Lines.Count} of {_all.Count} lines";

    public bool IsEmpty => !IsBusy && Lines.Count == 0 && !HasError;

    public string EmptyText => _content is null && _request is not null && !_request.Backup.IsText
        ? "This backup is stored as binary. Only Unimus can show it."
        : "No lines match.";

    public bool CanShare => _content is not null;

    public Task LoadAsync(UnimusBackupRequest request)
    {
        _request = request;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));

        return RunAsync(async () =>
        {
            // The list usually carries the content; when it doesn't, Unimus
            // has no single-backup endpoint, so it's found on its page.
            _content = request.Backup.Content;
            if (_content is null && request.Backup.IsText)
            {
                _content = await _unimus.GetBackupContentAsync(request.UnimusDeviceId, request.Backup.Id, 0, UnimusBackupsViewModel.PageSize);
            }

            _all = _content is null ? [] : UnimusLine.FromContent(_content);
            ApplySearch();
            OnPropertyChanged(nameof(CanShare));
            ShareCommand.NotifyCanExecuteChanged();
        });
    }

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
    private Task ShareAsync()
    {
        if (_content is null || _request is null)
        {
            return Task.CompletedTask;
        }

        var name = UnimusExport.FileNameFor(_request.DeviceName, _request.Backup.ValidSinceUtc?.ToLocalTime(), ".cfg");
        return _share.ShareTextFileAsync(name, _content, "text/plain", "Share backup");
    }
}
