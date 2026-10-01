using System.Windows.Input;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Press and hold on a view runs <see cref="CommandProperty"/> - how a list
/// row starts selecting several (#85), as Mail and Photos do. MAUI has no
/// long-press gesture, so each platform's own is attached to the row:
/// UIKit's long-press recogniser, and Android's gesture detector alongside
/// MAUI's own touch handling.
/// </summary>
/// <remarks>
/// The row's ordinary tap still works. Once a press is held long enough to
/// count, that touch is no longer a tap on either platform, so holding a
/// row doesn't also open it.
/// </remarks>
public static class LongPress
{
    public static readonly BindableProperty CommandProperty = BindableProperty.CreateAttached(
        "Command", typeof(ICommand), typeof(LongPress), null, propertyChanged: OnCommandChanged);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.CreateAttached(
        "CommandParameter", typeof(object), typeof(LongPress), null);

    public static ICommand? GetCommand(BindableObject view) => (ICommand?)view.GetValue(CommandProperty);

    public static void SetCommand(BindableObject view, ICommand? value) => view.SetValue(CommandProperty, value);

    public static object? GetCommandParameter(BindableObject view) => view.GetValue(CommandParameterProperty);

    public static void SetCommandParameter(BindableObject view, object? value) => view.SetValue(CommandParameterProperty, value);

    private static void OnCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view)
        {
            return;
        }

        view.HandlerChanged -= OnHandlerChanged;
        if (newValue is not null)
        {
            view.HandlerChanged += OnHandlerChanged;
            Attach(view);
        }
    }

    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is View view)
        {
            Attach(view);
        }
    }

    /// <summary>Held: a little bump, then the command with its parameter.</summary>
    private static void Fire(View view)
    {
        var command = GetCommand(view);
        var parameter = GetCommandParameter(view);
        if (command?.CanExecute(parameter) != true)
        {
            return;
        }

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        }
        catch (Exception)
        {
            // Android without the vibrate permission: no bump, still selects.
        }

        command.Execute(parameter);
    }

#if IOS
    private const string Tag = "DashyNMS.LongPress";

    private static void Attach(View view)
    {
        if (view.Handler?.PlatformView is not UIKit.UIView platform
            || platform.GestureRecognizers?.Any(g => g.Name == Tag) == true)
        {
            return;
        }

        var recogniser = new UIKit.UILongPressGestureRecognizer(r =>
        {
            if (r.State == UIKit.UIGestureRecognizerState.Began)
            {
                Fire(view);
            }
        })
        {
            Name = Tag,
            MinimumPressDuration = 0.45,
        };
        platform.UserInteractionEnabled = true;
        platform.AddGestureRecognizer(recogniser);
    }
#elif ANDROID
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Android.Views.View, Android.Views.GestureDetector> Detectors = new();

    private static void Attach(View view)
    {
        if (view.Handler?.PlatformView is not Android.Views.View platform || Detectors.TryGetValue(platform, out _))
        {
            return;
        }

        // Alongside MAUI's own handler on the same Touch event, which the
        // taps go through - this only listens, leaving "handled" as MAUI sets it.
        var detector = new Android.Views.GestureDetector(platform.Context, new HoldListener(() => Fire(view)));
        Detectors.Add(platform, detector);
        platform.Touch += (_, e) =>
        {
            if (e.Event is { } motion)
            {
                detector.OnTouchEvent(motion);
            }
        };
    }

    private sealed class HoldListener(Action held) : Android.Views.GestureDetector.SimpleOnGestureListener
    {
        public override bool OnDown(Android.Views.MotionEvent e) => true;

        public override void OnLongPress(Android.Views.MotionEvent e) => held();
    }
#else
    private static void Attach(View view)
    {
    }
#endif
}
