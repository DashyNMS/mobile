using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>Line icons on the 24px grid the tab bar's use, drawn as paths so they follow the theme.</summary>
public static class Icons
{
    public const string Back = "M15 5 8 12 15 19";

    public const string Pin = "M9 3H15L14 9L18 12V14H6V12L10 9Z M12 14V21";

    public const string Export = "M12 3V15 M7 8 12 3 17 8 M5 13V19A2 2 0 0 0 7 21H17A2 2 0 0 0 19 19V13";

    public const string Rediscover = "M20 11A8 8 0 1 0 17.7 16.7 M20 4V11H13";

    public const string Maintenance = "M14.7 6.3A4 4 0 0 0 9.3 11.7L3 18V21H6L12.3 14.7A4 4 0 0 0 17.7 9.3L15.2 11.8 12.8 11.2 12.2 8.8Z";

    public const string Ssh = "M4 17 10 12 4 7 M12 19H20";

    public const string Telnet = "M3 5H21V19H3Z M7 10 10 12 7 14 M12 15H17";
}

/// <summary>How an <see cref="IconButton"/> draws.</summary>
public enum IconButtonKind
{
    /// <summary>On the alt surface, icon and text: Rediscover, Maintenance.</summary>
    Filled,

    /// <summary>No background, in the accent colour: the back button, pin, export.</summary>
    Plain,
}

/// <summary>
/// A button with a line icon and optional text (Batch 10: Export,
/// the pin and Device View's actions as glyphs). A Border with a tap rather
/// than a Button, as a Button can't draw a path in the theme's colour.
/// </summary>
public sealed class IconButton : Border
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(IconButton), string.Empty,
        propertyChanged: (view, _, value) => ((IconButton)view).SetIcon(value as string));

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(IconButton), null,
        propertyChanged: (view, _, value) => ((IconButton)view).SetText(value as string));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconButton),
        propertyChanged: (view, old, value) => ((IconButton)view).CommandChanged(old as ICommand, value as ICommand));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(IconButton));

    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(IconButtonKind), typeof(IconButton), IconButtonKind.Filled,
        propertyChanged: (view, _, _) => ((IconButton)view).ApplyKind());

    /// <summary>Filled in the accent colour, as the pin is when the device is pinned.</summary>
    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(
        nameof(IsOn), typeof(bool), typeof(IconButton), false,
        propertyChanged: (view, _, _) => ((IconButton)view).ApplyKind());

    private readonly Microsoft.Maui.Controls.Shapes.Path _icon = new()
    {
        StrokeThickness = 1.8,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        WidthRequest = 24,
        HeightRequest = 24,
        Aspect = Stretch.None,
        VerticalOptions = LayoutOptions.Center,
        Scale = 0.8,
    };

    private readonly Label _text = new()
    {
        FontFamily = "BodySemibold",
        FontSize = 14,
        VerticalOptions = LayoutOptions.Center,
        IsVisible = false,
    };

    public IconButton()
    {
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 12 };
        MinimumHeightRequest = 44;
        MinimumWidthRequest = 44;
        Content = new HorizontalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _icon, _text },
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        };
        GestureRecognizers.Add(tap);

        ApplyKind();
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged += (_, _) => ApplyKind();
        }
    }

    /// <summary>One of <see cref="Icons"/>: SVG path data on a 24px grid.</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public IconButtonKind Kind
    {
        get => (IconButtonKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>Dimmed and untappable while the command can't run - Rediscover before the device has loaded.</summary>
    private void CommandChanged(ICommand? old, ICommand? command)
    {
        if (old is not null)
        {
            old.CanExecuteChanged -= OnCanExecuteChanged;
        }

        if (command is not null)
        {
            command.CanExecuteChanged += OnCanExecuteChanged;
        }

        OnCanExecuteChanged(this, EventArgs.Empty);
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        var enabled = Command?.CanExecute(CommandParameter) ?? true;
        IsEnabled = enabled;
        Opacity = enabled ? 1 : 0.4;
    }

    private void SetIcon(string? data) =>
        _icon.Data = string.IsNullOrEmpty(data) ? null : (Geometry?)new PathGeometryConverter().ConvertFromInvariantString(data);

    private void SetText(string? text)
    {
        _text.Text = text;
        _text.IsVisible = !string.IsNullOrEmpty(text);
        if (string.IsNullOrEmpty(SemanticProperties.GetDescription(this)))
        {
            SemanticProperties.SetDescription(this, text);
        }
    }

    private void ApplyKind()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var accent = Resource(dark ? "PrimaryTextDark" : "PrimaryTextLight");
        var text = Resource(dark ? "TextDark" : "TextLight");

        if (Kind == IconButtonKind.Plain)
        {
            BackgroundColor = Colors.Transparent;
            Padding = new Thickness(6, 0);
            _icon.Stroke = accent;
            _text.TextColor = accent;
            _text.FontSize = 16;
            _text.FontFamily = "BodyRegular";
        }
        else
        {
            BackgroundColor = Resource(dark ? "SurfaceAltDark" : "SurfaceAltLight");
            Padding = new Thickness(12, 0, 14, 0);
            _icon.Stroke = text;
            _text.TextColor = text;
        }

        _icon.Fill = IsOn ? accent : Colors.Transparent;
        if (IsOn)
        {
            _icon.Stroke = accent;
        }
    }

    private static Color Resource(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Gray;
}
