using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// The dashboard's toast (#140): "Wireless removed · Undo". Bound to the
/// app's one <see cref="Dashboard.DashboardToast"/>, on the dashboard and
/// on Edit dashboard.
/// </summary>
public partial class DashboardToastView : ContentView
{
    public static readonly BindableProperty ShowingProperty = BindableProperty.Create(
        nameof(Showing), typeof(bool), typeof(DashboardToastView), false,
        propertyChanged: (view, _, value) => ((DashboardToastView)view).Show((bool)value));

    public bool Showing
    {
        get => (bool)GetValue(ShowingProperty);
        set => SetValue(ShowingProperty, value);
    }

    /// <summary>Keep the toast present until its exit finishes, including when Undo dismisses it.</summary>
    private void Show(bool showing)
    {
        this.AbortAnimation("Toast");
        if (ReducedMotion.IsOn)
        {
            IsVisible = showing;
            Opacity = 1;
            TranslationY = 0;
            return;
        }
        if (showing && !IsVisible)
        {
            Opacity = 0;
            TranslationY = 24;
            IsVisible = true;
        }
        var opacity = Opacity;
        var translation = TranslationY;
        this.Animate("Toast", progress =>
        {
            Opacity = opacity + ((showing ? 1 : 0) - opacity) * progress;
            TranslationY = translation + ((showing ? 0 : 24) - translation) * progress;
        }, length: 220, easing: showing ? Easing.CubicOut : Easing.CubicIn,
        finished: (_, cancelled) =>
        {
            if (!cancelled && !Showing) IsVisible = false;
        });
    }

	public DashboardToastView()
	{
		InitializeComponent();
        SetBinding(ShowingProperty, "IsShowing");
	}
}
