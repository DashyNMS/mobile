using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace DashyNMS.Mobile;

/// <summary>
/// Every text box and picker as the app's field well (Styles.xaml gives them
/// the alt surface): without Android's underline, which drew a second
/// outline under the well, and with room inside the edges.
/// </summary>
internal static class FieldWells
{
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Well(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Well(handler.PlatformView));
        PickerHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Well(handler.PlatformView));
        DatePickerHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Well(handler.PlatformView));
        TimePickerHandler.Mapper.AppendToMapping("FieldWell", (handler, _) => Well(handler.PlatformView));
    }

    private static void Well(Android.Views.View view)
    {
        view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        var inset = (int)view.Context.ToPixels(12);
        view.SetPadding(inset, view.PaddingTop, inset, view.PaddingBottom);
    }
}
