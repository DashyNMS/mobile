using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Adapters;

/// <summary>Uses the platform's paginated print renderer; no browser or network content is loaded.</summary>
public sealed class IntelligencePdfPrinter : IIntelligencePdfPrinter
{
    public Task<byte[]> PrintAsync(ExportedChat chat) => MainThread.InvokeOnMainThreadAsync(() =>
    {
#if IOS
        var bounds = new CoreGraphics.CGRect(0, 0, 595.28, 841.89);
        var printable = new CoreGraphics.CGRect(54, 54, 487.28, 733.89);
        using var formatter = new UIKit.UIMarkupTextPrintFormatter(IntelligenceChatPdfHtml.Write(chat));
        using var renderer = new UIKit.UIPrintPageRenderer();
        renderer.SetValueForKey(Foundation.NSValue.FromCGRect(bounds), new Foundation.NSString("paperRect"));
        renderer.SetValueForKey(Foundation.NSValue.FromCGRect(printable), new Foundation.NSString("printableRect"));
        renderer.AddPrintFormatter(formatter, 0);
        renderer.PrepareForDrawingPages(new Foundation.NSRange(0, 1));
        using var pdf = new UIKit.UIGraphicsPdfRenderer(bounds);
        using var data = pdf.CreatePdf(context =>
        {
            for (nint page = 0; page < renderer.NumberOfPages; page++)
            {
                context.BeginPage();
                renderer.DrawPage(page, bounds);
            }
        });
        return data.ToArray();
#else
        throw new PlatformNotSupportedException("PDF export needs the iOS Intelligence route.");
#endif
    });
}
