using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One row of the device list.</summary>
public sealed partial class DeviceItem : ObservableObject
{
    private readonly DeviceNameStyle _nameStyle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinText))]
    private bool _isPinned;

    /// <summary>Inside a maintenance window - a per-device lookup, filled in after the list loads.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State), nameof(StateText))]
    private bool _isUnderMaintenance;

    public DeviceItem(Device device, DeviceNameStyle nameStyle = DeviceNameStyle.Hostname)
    {
        Device = device;
        _nameStyle = nameStyle;
    }

    public Device Device { get; }

    /// <summary>The swipe action's label.</summary>
    public string PinText => IsPinned ? "Unpin" : "Pin";

    public int DeviceId => Device.DeviceId;

    /// <summary>Following desktop's hostname / sysName / display name preference.</summary>
    public string Name => _nameStyle.Resolve(Device, Device.Hostname);

    /// <summary>The other name, when it isn't just the same again.</summary>
    public string? AlternateName => _nameStyle.ResolveSecondary(Device, Device.Hostname, Name);

    /// <summary>"10.0.0.1 · Cisco C9300 · London DC" - whichever of those are known.</summary>
    public string Details => string.Join(" · ", new[] { Device.Ip, Device.Hardware, Device.Location }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>As desktop: maintenance takes over from up/down while a window is open.</summary>
    public DeviceState State => IsUnderMaintenance ? DeviceState.Maintenance : Device.State;

    public string StateText => State.ToDisplayString();

    /// <summary>Uptime means nothing for a device that isn't up, so desktop shows a dash.</summary>
    public string UptimeText => Device.State == DeviceState.Up ? Formatting.Uptime(Device.Uptime) : "—";

    /// <summary>The fields desktop's device search looks in.</summary>
    public bool Matches(string term) =>
        Contains(Name, term)
        || Contains(AlternateName, term)
        || Contains(Device.Hostname, term)
        || Contains(Device.Ip, term)
        || Contains(Device.Os, term)
        || Contains(Device.Hardware, term)
        || Contains(Device.Location, term)
        || Contains(Device.Type, term)
        || Contains(Device.Contact, term)
        || Contains(Device.Serial, term)
        || DeviceId.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal);

    private static bool Contains(string? field, string text) =>
        field?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
}
