using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Adapters;

/// <summary>Uses the platform's paginated print renderer; no browser or network content is loaded.</summary>
public sealed class IntelligencePdfPrinter : IIntelligencePdfPrinter
{
    public Task<byte[]> PrintAsync(ExportedChat chat)
    {
#if IOS
        return MainThread.InvokeOnMainThreadAsync((Func<byte[]>)(() =>
            global::DashyNMS.Mobile.Platforms.iOS.IntelligencePdfRenderer.Render(chat)));
#else
        return Task.FromException<byte[]>(new PlatformNotSupportedException("PDF export needs the iOS Intelligence route."));
#endif
    }
}
