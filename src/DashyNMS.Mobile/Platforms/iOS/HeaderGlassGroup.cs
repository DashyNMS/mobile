using Microsoft.Maui.Handlers;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// A compact Liquid Glass group of actions laid out by the page, rather than
/// placed over the tab bar as <see cref="TabBarButtons"/> are: the chat puts
/// Saved chats and New chat beside its title on a big portrait screen (#594).
/// </summary>
/// <remarks>
/// It looks like the tab bar's group (<see cref="GlassStyle"/>) and is a
/// fixed 44pt per action, so the header measures it without asking UIKit,
/// and the title's column keeps whatever is left. Each action is a native
/// button, so VoiceOver, Full Keyboard Access and pointer hover reach it as
/// they would any other button.
/// </remarks>
internal sealed class HeaderGlassGroup : View
{
    /// <summary>The side of each action's square: the app's minimum touch target.</summary>
    public const double Size = 44;

    public static readonly BindableProperty ActionsProperty = BindableProperty.Create(
        nameof(Actions), typeof(IReadOnlyList<GlassAction>), typeof(HeaderGlassGroup), Array.Empty<GlassAction>(),
        propertyChanged: (bindable, _, value) =>
        {
            var group = (HeaderGlassGroup)bindable;
            var count = ((IReadOnlyList<GlassAction>)value).Count;
            group.WidthRequest = Size * count;
            group.HeightRequest = Size;
        });

    public HeaderGlassGroup()
    {
        WidthRequest = 0;
        HeightRequest = Size;
    }

    public IReadOnlyList<GlassAction> Actions
    {
        get => (IReadOnlyList<GlassAction>)GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}

/// <summary>Draws a <see cref="HeaderGlassGroup"/> as glass holding one button per action.</summary>
internal sealed class HeaderGlassGroupHandler : ViewHandler<HeaderGlassGroup, UIVisualEffectView>
{
    public static readonly PropertyMapper<HeaderGlassGroup, HeaderGlassGroupHandler> Mapper = new(ViewMapper)
    {
        [nameof(HeaderGlassGroup.Actions)] = (handler, _) => handler.Rebuild(),
    };

    private readonly UIStackView _row = new()
    {
        Axis = UILayoutConstraintAxis.Horizontal,
        Distribution = UIStackViewDistribution.FillEqually,
        Alignment = UIStackViewAlignment.Fill,
        AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
    };

    public HeaderGlassGroupHandler() : base(Mapper)
    {
    }

    // Not interactive: interactive glass keeps a button inside it from
    // hearing the tap, as the tab bar's action group found.
    protected override UIVisualEffectView CreatePlatformView()
    {
        var glass = new UIVisualEffectView(GlassStyle.Effect(false)) { ClipsToBounds = true };
        _row.Frame = glass.ContentView.Bounds;
        glass.ContentView.AddSubview(_row);
        return glass;
    }

    public override void PlatformArrange(Rect rect)
    {
        base.PlatformArrange(rect);
        PlatformView.Layer.CornerRadius = (nfloat)(Math.Min(rect.Width, rect.Height) / 2);
        _row.Frame = PlatformView.ContentView.Bounds;
    }

    private void Rebuild()
    {
        foreach (var old in _row.ArrangedSubviews)
        {
            _row.RemoveArrangedSubview(old);
            old.RemoveFromSuperview();
        }

        foreach (var action in VirtualView.Actions)
        {
            _row.AddArrangedSubview(Button(action));
        }
    }

    private static UIButton Button(GlassAction action)
    {
        var configuration = UIButtonConfiguration.PlainButtonConfiguration;
        configuration.CornerStyle = UIButtonConfigurationCornerStyle.Capsule;
        configuration.ContentInsets = new NSDirectionalEdgeInsets(0, 0, 0, 0);
        configuration.Image = GlassStyle.Icon(action.Symbol).Image;
        configuration.BaseForegroundColor = UIColor.Label;
        var button = new UIButton(UIButtonType.System)
        {
            Configuration = configuration,
            BackgroundColor = UIColor.Clear,
            AccessibilityLabel = action.Label,
            Enabled = action.IsEnabled,
        };
        // A UIAction rather than an event, so the button holds no handler to unhook when rebuilt.
        button.AddAction(UIAction.Create(_ => action.Tapped()), UIControlEvent.TouchUpInside);
        return button;
    }
}
