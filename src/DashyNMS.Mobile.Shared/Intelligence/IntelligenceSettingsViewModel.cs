using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Availability and automatic-save settings for the on-device model, without third-party consent.</summary>
/// <remarks>
/// Single-chat mode only stops saving; it never deletes. Delete saved chats
/// (#230) is the separate choice for that, offered whether or not the mode is
/// on, and it reports what was actually removed: a partial removal says so and
/// offers to try again, and is never shown as done.
/// </remarks>
public sealed partial class IntelligenceSettingsViewModel(
    IIntelligenceModel model, IntelligencePreferences preferences, IDialogService dialogs, ILauncherService launcher,
    ISavedChatsDeletion? savedChats = null) : ViewModelBase
{
    internal const string DeleteChatsTitle = "Delete saved chats?";
    internal const string DeleteChatsAction = "Delete saved chats";
    internal const string DeleteChatsMessage = "Every saved Intelligence chat on this phone, for every server, is deleted with the key that protects them. "
        + "The chat on screen stays until you leave it, but isn't saved. " + Confirmations.CannotBeUndone;
    internal const string DeletedText = "Saved chats were deleted.";
    internal const string DeletingText = "Deleting saved chats…";
    internal const string NotDeletedTitle = "Saved chats could not all be deleted";
    internal const string NotDeletedText = "Saved chats could not all be deleted. What's left stays locked, even after the app restarts, and deleting is tried again before saved chats are next used.";
    internal const string NotDeletedUntilRestartText = "Saved chats could not all be deleted, and the note keeping them locked couldn't be saved. They're locked only until the app closes; after a restart they could be readable again.";
    internal const string NotStartedText = "Saved chats weren't deleted. Try again.";
    internal const string TryAgainChoice = "Try again";
    internal const string NotNowChoice = "Not now";

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

    /// <summary>Delete saved chats is offered: chats can be kept on this phone, and none is being deleted now.</summary>
    public bool CanDeleteSavedChats => savedChats is not null && !_deletingChats;

    /// <summary>The button is shown only where chats can be saved at all.</summary>
    public bool HasSavedChats => savedChats is not null;

    private bool _deletingChats;

    private string _savedChatsStatus = "";
    /// <summary>What the last Delete saved chats did, in words; empty until it is used.</summary>
    public string SavedChatsStatus { get => _savedChatsStatus; private set { if (SetProperty(ref _savedChatsStatus, value)) OnPropertyChanged(nameof(HasSavedChatsStatus)); } }

    public bool HasSavedChatsStatus => SavedChatsStatus.Length > 0;

    private bool _savedChatsFailed;
    /// <summary>The last Delete saved chats left something behind, so the status reads as an error.</summary>
    public bool SavedChatsFailed { get => _savedChatsFailed; private set => SetProperty(ref _savedChatsFailed, value); }

    /// <summary>
    /// Asks, then deletes every saved chat and their key. A partial removal
    /// offers Try again until it succeeds or the person stops; either way the
    /// status says what is true afterwards.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSavedChats))]
    private async Task DeleteSavedChatsAsync()
    {
        if (savedChats is null || _deletingChats) return;
        SetDeleting(true);
        try
        {
            if (!await dialogs.ConfirmDestructiveAsync(DeleteChatsTitle, DeleteChatsMessage, DeleteChatsAction)) return;
            while (true)
            {
                SavedChatsFailed = false;
                SavedChatsStatus = DeletingText;
                var result = await savedChats.DeleteSavedChatsAsync();
                if (result == ChatWipeResult.Removed)
                {
                    SavedChatsStatus = DeletedText;
                    return;
                }

                var text = result == ChatWipeResult.Retained ? NotDeletedText : NotDeletedUntilRestartText;
                SavedChatsFailed = true;
                SavedChatsStatus = text;
                if (!await dialogs.ConfirmAsync(NotDeletedTitle, text, TryAgainChoice, NotNowChoice)) return;
            }
        }
        catch
        {
            // A question that couldn't be shown: nothing was deleted on its say-so.
            if (!SavedChatsFailed)
            {
                SavedChatsFailed = true;
                SavedChatsStatus = NotStartedText;
            }
        }
        finally { SetDeleting(false); }
    }

    private void SetDeleting(bool deleting)
    {
        _deletingChats = deleting;
        OnPropertyChanged(nameof(CanDeleteSavedChats));
        DeleteSavedChatsCommand.NotifyCanExecuteChanged();
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
