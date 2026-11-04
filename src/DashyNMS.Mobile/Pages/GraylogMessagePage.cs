using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Pushed message host; embedded messages retain their list's glass owner.</summary>
public sealed class GraylogMessagePage : ContentPage, IQueryAttributable, IActivePage
{
    private readonly GraylogMessageView _view;
    private IDisposable? _edges;
    private bool _showing;
    private GraylogMessageViewModel ViewModel => _view.ViewModel;
#if IOS
    private (bool Filter, bool Device, string FilterText, string DeviceText)? _actions;
#endif

    public GraylogMessagePage(GraylogMessageViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Message";
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#else
        SafeAreaEdges = SafeAreaEdges.All;
#endif
        Content = _view = new GraylogMessageView(viewModel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Restores retained message controls through AppShell's single activation owner.</summary>
    public void Activate()
    {
#if IOS
        _actions = null;
#endif
        _showing = true;
        _view.Bar.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
        ViewModel.PropertyChanged -= OnMessageChanged;
        ViewModel.PropertyChanged += OnMessageChanged;
        ViewModel.OpenDeviceCommand.CanExecuteChanged -= OnCommandChanged;
        ViewModel.OpenDeviceCommand.CanExecuteChanged += OnCommandChanged;
        ViewModel.ShowOnlyCommand.CanExecuteChanged -= OnCommandChanged;
        ViewModel.ShowOnlyCommand.CanExecuteChanged += OnCommandChanged;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        RestoreGlass();
        PlaceControls();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _showing = false;
        ViewModel.PropertyChanged -= OnMessageChanged;
        ViewModel.OpenDeviceCommand.CanExecuteChanged -= OnCommandChanged;
        ViewModel.ShowOnlyCommand.CanExecuteChanged -= OnCommandChanged;
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void RestoreGlass()
    {
#if IOS
        _view.Bar.IsVisible = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable) _view.HideInlineActions();
        if (TabBarButtons.IsAvailable)
        {
            var state = (ViewModel.ShowOnlyCommand.CanExecute(null), ViewModel.OpenDeviceCommand.CanExecute(null), ViewModel.ShowOnlyText, ViewModel.DeviceActionDescription);
            if (_actions == state) return;
            _actions = state;
            List<GlassAction> actions = [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => { if (IsActive) _ = Shell.Current.GoToAsync(".."); }, IsBack: true)];
            if (state.Item1)
                actions.Add(new("line.3.horizontal.decrease", ViewModel.ShowOnlyText, () =>
                {
                    if (IsActive && ViewModel.ShowOnlyCommand.CanExecute(null)) ViewModel.ShowOnlyCommand.Execute(null);
                }));
            if (state.Item2)
                actions.Add(new("desktopcomputer", ViewModel.DeviceActionDescription, () =>
                {
                    if (IsActive && ViewModel.OpenDeviceCommand.CanExecute(null)) ViewModel.OpenDeviceCommand.Execute(null);
                }));
            TabBarButtons.Show(this, null, string.Empty,
                actions);
        }
#endif
    }

    private bool IsActive => _showing && Shell.Current?.CurrentPage == this;

    private void OnCommandChanged(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(() => { if (IsActive) { RestoreGlass(); PlaceControls(); } });

    private void OnMessageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Dispatch(() => { if (IsActive) { RestoreGlass(); PlaceControls(); } });
    }

    private void PlaceControls()
    {
        if (IsActive) _view.PlaceContent(CoveredEdges.ForScrolling(this));
    }
#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif
    public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
