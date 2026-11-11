using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Availability and automatic-save settings for the on-device model, without third-party consent.</summary>
public sealed partial class IntelligenceSettingsViewModel(
    IIntelligenceModel model, IntelligencePreferences preferences, IDialogService dialogs, ILauncherService launcher) : ViewModelBase
{
    internal const string Notice = "Answers are worked out on this iPhone or iPad. Your question and what Intelligence looks up in LibreNMS are processed on this device. Nothing is sent to DashyNMS or a third-party AI service. LibreNMS is contacted to read your network data. Answers can be wrong; check the source before acting.";
    public bool IsVisible => model.Availability != IntelligenceAvailability.Unsupported;
    public bool IsAvailable => model.Availability == IntelligenceAvailability.Available;
    public bool CanUseIntelligence => IsAvailable && preferences.Enabled;
    public bool Enabled => preferences.Enabled;
    public string ToggleText => Enabled ? "Turn off Intelligence" : "Turn on Intelligence";
    public bool CanOpenSettings => model.Availability == IntelligenceAvailability.NotEnabled;
    public string AvailabilityText => model.Availability switch
    {
        IntelligenceAvailability.Available => "Ready to answer on this device.",
        IntelligenceAvailability.Unsupported => "Intelligence needs iOS 26 or later.",
        IntelligenceAvailability.DeviceNotEligible => "This device doesn't support Apple Intelligence.",
        IntelligenceAvailability.NotEnabled => "Apple Intelligence is turned off. Open Settings and turn it on in Apple Intelligence & Siri.",
        IntelligenceAvailability.ModelNotReady => "Apple Intelligence is still downloading or preparing its model. Try again later.",
        _ => "Apple Intelligence is unavailable. Try again later.",
    };
    public IReadOnlyList<string> DetailLabels { get; } = ["Brief", "Balanced", "Detailed"];
    private static readonly string[] Details = [IntelligenceDetail.Brief, IntelligenceDetail.Balanced, IntelligenceDetail.Detailed];
    public int DetailIndex
    {
        get => Array.IndexOf(Details, preferences.Detail);
        set { if (value >= 0 && value < Details.Length) Save(() => preferences.SetDetail(Details[value])); }
    }
    public bool ColourStatusIndicators
    {
        get => preferences.ColourStatusIndicators;
        set => Save(() => preferences.SetColourStatusIndicators(value));
    }
    public bool SingleChatMode
    {
        get => preferences.SingleChatMode;
        set => Save(() => preferences.SetSingleChatMode(value));
    }

    /// <summary>Availability can change while the person is in the system Settings app.</summary>
    public void Refresh()
    {
        foreach (var name in new[] { nameof(IsVisible), nameof(IsAvailable), nameof(CanUseIntelligence), nameof(Enabled), nameof(ToggleText), nameof(CanOpenSettings), nameof(AvailabilityText), nameof(DetailIndex), nameof(ColourStatusIndicators), nameof(SingleChatMode) })
            OnPropertyChanged(name);
        ToggleCommand.NotifyCanExecuteChanged();
    }

    private bool CanToggle() => Enabled || IsAvailable;

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private async Task ToggleAsync()
    {
        if (!Enabled)
        {
            if (!IsAvailable) return;
            if (!preferences.NoticeAccepted && !await dialogs.ConfirmAsync("Intelligence on this device", Notice, "Turn on", "Cancel")) return;
            // Recheck after the notice, since system availability may have changed.
            if (!IsAvailable) { Refresh(); return; }
        }
        Save(() => preferences.SetEnabled(!Enabled));
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (!CanOpenSettings) return;
        try
        {
            // The public app-settings URL opens Settings; no private Apple Intelligence URL is used.
            if (!await launcher.TryOpenAppAsync(new Uri("app-settings:")))
                ErrorMessage = "Open the Settings app and choose Apple Intelligence & Siri.";
        }
        catch { ErrorMessage = "Open the Settings app and choose Apple Intelligence & Siri."; }
    }

    private void Save(Action save)
    {
        ErrorMessage = null;
        try { save(); }
        catch { ErrorMessage = "The Intelligence setting couldn't be saved. Try again."; }
        Refresh();
    }
}
