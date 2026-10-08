using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Licences;

namespace DashyNMS.Mobile.Adapters;

/// <summary>Which phone this is, so the licences page lists Android's extras only on Android (#164).</summary>
public sealed class NoticePlatform : INoticePlatform
{
    public NoticePlatforms Platform =>
        DeviceInfo.Current.Platform == DevicePlatform.Android ? NoticePlatforms.Android : NoticePlatforms.iOS;
}
