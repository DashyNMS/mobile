using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Platforms.iOS;

/// <summary>The production renderer, also compiled into the simulator PDF regression harness.</summary>
public static class IntelligencePdfRenderer
{
    public static byte[] Render(ExportedChat chat)
    {
        var bounds = new CoreGraphics.CGRect(0, 0, 595.28, 841.89);
        var printable = new CoreGraphics.CGRect(54, 54, 487.28, 733.89);
        // UIMarkupTextPrintFormatter preserves text and links but does not embed data-URI
        // images. Keep their answer association in text and draw a labelled native appendix.
        var graphs = new List<(int Answer, int Number, ExportedChatImage Image)>();
        var answer = 0;
        var messages = chat.Messages.Select(message =>
        {
            if (!message.IsUser) answer++;
            for (var index = 0; index < message.Images.Count; index++)
                graphs.Add((answer, index + 1, message.Images[index]));
            return message with
            {
                Images = [],
                Markdown = message.Markdown + (message.Images.Count > 0
                    ? $"\n\nGraph attachments for answer {answer} are in the graph appendix." : ""),
            };
        }).ToArray();
        using var formatter = new UIKit.UIMarkupTextPrintFormatter(IntelligenceChatPdfHtml.Write(chat with { Messages = messages }));
        using var renderer = new UIKit.UIPrintPageRenderer();
        renderer.SetValueForKey(Foundation.NSValue.FromCGRect(bounds), new Foundation.NSString("paperRect"));
        renderer.SetValueForKey(Foundation.NSValue.FromCGRect(printable), new Foundation.NSString("printableRect"));
        renderer.AddPrintFormatter(formatter, 0);
        renderer.PrepareForDrawingPages(new Foundation.NSRange(0, 1));
        using var format = new UIKit.UIGraphicsPdfRendererFormat();
        using var pdf = new UIKit.UIGraphicsPdfRenderer(bounds, format);
        using var data = pdf.CreatePdf(context =>
        {
            for (nint page = 0; page < renderer.NumberOfPages; page++)
            {
                context.BeginPage();
                renderer.DrawPage(page, bounds);
            }
            foreach (var graph in graphs)
            {
                using var bytes = Foundation.NSData.FromArray(graph.Image.Data);
                using var image = UIKit.UIImage.LoadFromData(bytes)
                    ?? throw new InvalidDataException("The graph image could not be decoded.");
                if (image.Size.Width <= 0 || image.Size.Height <= 0)
                    throw new InvalidDataException("The graph image has no drawable size.");
                context.BeginPage();
                using var heading = new Foundation.NSString($"Answer {graph.Answer} · Graph {graph.Number}");
                heading.DrawString(new CoreGraphics.CGPoint(54, 54), new UIKit.UIStringAttributes
                    { Font = UIKit.UIFont.SystemFontOfSize(12), ForegroundColor = UIKit.UIColor.Black });
                var scale = Math.Min(printable.Width / image.Size.Width, (printable.Height - 36) / image.Size.Height);
                image.Draw(new CoreGraphics.CGRect(54, 90, image.Size.Width * scale, image.Size.Height * scale));
            }
        });
        return data.ToArray();
    }
}
