using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Mobile opt-in gate over Core's settings, whose desktop default is enabled.</summary>
public sealed class IntelligencePreferences(ISettingsStore settings, IAppPreferences preferences)
{
    internal const string NoticeKey = "intelligence.ondevice.notice.v1";
    public bool NoticeAccepted => preferences.Get(NoticeKey) == "accepted";
    public bool Enabled => NoticeAccepted && settings.Current.IntelligenceEnabled;
    public string Detail => IntelligenceDetail.Normalize(settings.Current.IntelligenceReplyStyle) is IntelligenceDetail.Full
        ? IntelligenceDetail.Detailed : IntelligenceDetail.Normalize(settings.Current.IntelligenceReplyStyle);
    public bool ColourStatusIndicators => settings.Current.IntelligenceColourStatusIndicators;
    public bool SingleChatMode => settings.Current.IntelligenceSingleChatMode;
    public event EventHandler? Changed;

    /// <summary>Records the on-device notice separately from desktop's third-party consent.</summary>
    public void SetEnabled(bool enabled)
    {
        var previousNotice = preferences.Get(NoticeKey);
        var writeNotice = enabled && !NoticeAccepted;
        try
        {
            // Write the notice first, so its failure cannot leave a committed
            // enablement. Accepted notices need no second write on re-enabling.
            if (writeNotice) preferences.Set(NoticeKey, "accepted");
            Save(s => s.IntelligenceEnabled = enabled, notify: false);
        }
        catch
        {
            if (writeNotice) preferences.Set(NoticeKey, previousNotice);
            throw;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetDetail(string value) => Save(s => s.IntelligenceReplyStyle = value);
    public void SetColourStatusIndicators(bool value) => Save(s => s.IntelligenceColourStatusIndicators = value);
    public void SetSingleChatMode(bool value) => Save(s => s.IntelligenceSingleChatMode = value);

    /// <summary>Restores just these settings if persistence fails, so the page does not claim a saved change.</summary>
    private void Save(Action<AppSettings> change, bool notify = true)
    {
        var s = settings.Current;
        var enabled = s.IntelligenceEnabled;
        var detail = s.IntelligenceReplyStyle;
        var colour = s.IntelligenceColourStatusIndicators;
        var single = s.IntelligenceSingleChatMode;
        try
        {
            change(s);
            // A best-effort Save cannot acknowledge a durable opt-out. Fail
            // closed for stores without the explicit persistence contract.
            if (settings is not ICheckedSettingsStore checkedStore || !checkedStore.TrySave())
                throw new IOException("Settings could not be saved.");
        }
        catch
        {
            s.IntelligenceEnabled = enabled;
            s.IntelligenceReplyStyle = detail;
            s.IntelligenceColourStatusIndicators = colour;
            s.IntelligenceSingleChatMode = single;
            throw;
        }
        if (notify) Changed?.Invoke(this, EventArgs.Empty);
    }
}
