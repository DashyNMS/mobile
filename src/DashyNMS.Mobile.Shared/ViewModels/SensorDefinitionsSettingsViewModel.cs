using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Sensors;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// Settings › Sensor definitions (#209), as desktop's section: on or off, the
/// official catalogue or a public GitHub repository of your own, its version,
/// the last successful check, Core's status, and Update now.
/// </summary>
/// <remarks>
/// The switch and source are a draft until Save, so a half-typed repository
/// address never reconfigures the store.
/// </remarks>
public sealed partial class SensorDefinitionsSettingsViewModel : ViewModelBase
{
    private readonly SensorDefinitionStore _store;
    private readonly INavigationService _navigation;
    private readonly SynchronizationContext? _context;

    public SensorDefinitionsSettingsViewModel(SensorDefinitions definitions, INavigationService navigation)
    {
        _store = definitions.Store;
        _navigation = navigation;
        _context = SynchronizationContext.Current;
        Load();
    }

    /// <summary>
    /// The page showing: Core's progress followed only while it does - the
    /// store lives as long as the app, and this page doesn't.
    /// </summary>
    public void Attach()
    {
        _store.Changed -= OnStoreChanged;
        _store.Changed += OnStoreChanged;
        RaiseStore();
    }

    public void Detach() => _store.Changed -= OnStoreChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool _enabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText), nameof(CanUpdate))]
    private bool _useCustomRepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText), nameof(CanUpdate))]
    private string _customRepository = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool _isUpdating;

    public string SourceText => UseCustomRepository ? CustomRepository : SensorDefinitionOptions.OfficialRepository;

    public string StatusText => _store.Status;

    public string VersionText => _store.Version ?? "Not downloaded";

    public string LastCheckText => _store.LastSuccessfulCheck?.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture) ?? "Not checked";

    /// <summary>Update now: only for what's saved, not a draft still waiting for Save.</summary>
    public bool CanUpdate => !IsUpdating && _store.Options.Enabled && Draft == _store.Options;

    private SensorDefinitionOptions Draft => new()
    {
        Enabled = Enabled,
        UseCustomRepository = UseCustomRepository,
        CustomRepository = CustomRepository.Trim(),
    };

    private void Load()
    {
        var options = _store.Options;
        Enabled = options.Enabled;
        UseCustomRepository = options.UseCustomRepository;
        CustomRepository = options.CustomRepository;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var draft = Draft;
        if (draft == _store.Options)
        {
            await _navigation.GoToAsync(Routes.Back);
            return;
        }

        if (draft.UseCustomRepository)
        {
            // Refused before anything is saved: only a public HTTPS GitHub repository.
            draft = draft with { CustomRepository = SensorDefinitionOptions.NormaliseRepository(draft.CustomRepository) };
        }

        await _store.ConfigureAsync(draft);
        Load();
        if (draft.Enabled)
        {
            _ = _store.SynchroniseAsync(Array.Empty<string>());
        }

        await _navigation.GoToAsync(Routes.Back);
    }, isRead: false);

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (!CanUpdate)
        {
            return;
        }

        IsUpdating = true;
        try
        {
            await _store.SynchroniseAsync(force: true);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException)
        {
            // Core's status says what went wrong.
        }
        finally
        {
            IsUpdating = false;
            RaiseStore();
        }
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (_context is { } context)
        {
            context.Post(_ => RaiseStore(), null);
        }
        else
        {
            RaiseStore();
        }
    }

    private void RaiseStore()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(LastCheckText));
        OnPropertyChanged(nameof(CanUpdate));
    }
}
