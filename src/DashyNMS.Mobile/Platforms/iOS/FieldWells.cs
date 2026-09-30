using CoreGraphics;
using Microsoft.Maui.Handlers;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// Finishes off every text box and picker as the app's field well - the
/// look Sign in always had (Styles.xaml gives them the alt surface): no
/// UIKit rounded-rect border, which drew a white box with a grey edge on the
/// dark cards, 12pt corners like the cards, room inside the edges, and a
/// chevron on anything that opens a choice so it reads as tappable.
/// </summary>
internal static class FieldWells
{
    private const float Radius = 12;
    private const float Inset = 12;

    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => TextField(handler.PlatformView, chooses: false));
        PickerHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => TextField(handler.PlatformView, chooses: true));
        DatePickerHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Any(handler.PlatformView));
        TimePickerHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Any(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("FieldWell", (handler, _) =>
        {
            Round(handler.PlatformView);
            handler.PlatformView.TextContainerInset = new UIEdgeInsets(12, Inset - 4, 12, Inset - 4);
        });
    }

    /// <summary>The date and time pickers are text fields on some iOS versions, native pickers on others.</summary>
    private static void Any(UIView view)
    {
        if (view is UITextField field)
        {
            TextField(field, chooses: true);
        }
        else
        {
            Round(view);
        }
    }

    private static void TextField(UITextField field, bool chooses)
    {
        field.BorderStyle = UITextBorderStyle.None;
        Round(field);

        field.LeftView = new UIView(new CGRect(0, 0, Inset, 1));
        field.LeftViewMode = UITextFieldViewMode.Always;

        var right = new UIView(new CGRect(0, 0, chooses ? Inset + 16 : Inset, 20));
        if (chooses && UIImage.GetSystemImage("chevron.down", UIImageSymbolConfiguration.Create(12, UIImageSymbolWeight.Semibold)) is { } chevron)
        {
            right.AddSubview(new UIImageView(chevron)
            {
                Frame = new CGRect(0, 0, 16, 20),
                ContentMode = UIViewContentMode.Center,
                TintColor = UIColor.SecondaryLabel,
            });
        }

        field.RightView = right;
        field.RightViewMode = UITextFieldViewMode.Always;
    }

    private static void Round(UIView view)
    {
        view.Layer.CornerRadius = Radius;
        view.ClipsToBounds = true;
    }
}
