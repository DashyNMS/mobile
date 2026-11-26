using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Help;
using DashyNMS.Mobile.Search;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Network Search (#129), shown via its More route or pinned: the field, and
/// the answer under it. ⌘F and the glass magnifier put the cursor in the field.
/// </summary>
public partial class NetworkSearchPage : ContentPage, ISearchHost, IActivePage, IQueryAttributable
{
    private readonly NetworkSearchViewModel _viewModel;
    private IDisposable? _edges;
    private bool _active;
#if IOS
    private LiveKeyboardBounds? _keyboard;
#endif

    public NetworkSearchPage(NetworkSearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        MacLocations.SetValue(BindableLayout.ItemTemplateProperty, ConnectedLocationTemplate());
        SeenOn.SetValue(BindableLayout.ItemTemplateProperty, LocationTemplate());
        AddressRows.SetValue(BindableLayout.ItemTemplateProperty, new DataTemplate(() =>
        {
            var row = new CopyableRow();
            row.SetBinding(CopyableRow.ValueProperty, static (CopyableValueViewModel v) => v);
            return row;
        }));
        viewModel.Copied += (_, value) => SemanticScreenReader.Default.Announce("Copied " + value);
        viewModel.PropertyChanged += (_, e) =>
        {
            // Results arrive away from the field: say what came back, once, when the search ends.
            if (e.PropertyName == nameof(NetworkSearchViewModel.IsSearching) && !viewModel.IsSearching && _active)
            {
                Dispatcher.Dispatch(AnnounceResult);
            }

            if (e.PropertyName is nameof(NetworkSearchViewModel.IsSearching))
            {
                PullRefresh.IsRefreshing = false;
            }
        };
        Root.SizeChanged += (_, _) => PlaceControls();
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        Scroller.HandlerChanged += (_, _) => StopInsets();
        Scroller.Loaded += (_, _) => StopInsets();
#endif
    }

    /// <summary>A query from elsewhere - an address in a device's ARP table, say - is searched straight away.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.NetworkSearchQueryParameter, out var value) && value is string text && text.Length > 0)
        {
            _ = _viewModel.SearchForAsync(text);
        }

        query.Clear();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Restores controls without searching again or clearing the query.</summary>
    public void Activate()
    {
        _active = true;
        _viewModel.Attach();
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
        ManualKeyboardScroll.Acquire(this);
        _keyboard ??= new LiveKeyboardBounds(PlaceControls);
        ShowGlass();
#endif
        PlaceControls();
    }

#if IOS
    private void ShowGlass()
    {
        if (!_active || !TabBarButtons.IsAvailable) return;
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
        {
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => { if (_active) _ = Shell.Current.GoToAsync(".."); }, IsBack: true));
        }

        actions.Add(new("square.and.arrow.up", "Share the results as CSV", () => _viewModel.ShareCommand.Execute(null), IsEnabled: _viewModel.HasResults));
        TabBarButtons.Show(this, OpenSearch, "Search the network", actions);
    }
#endif

    public void OpenSearch()
    {
        if (_active) Search.Focus();
    }

    protected override void OnDisappearing()
    {
        _active = false;
        _viewModel.Detach();
        _edges?.Dispose();
        _edges = null;
        KeyboardDismiss.Close(Search);
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
        ManualKeyboardScroll.Release(this);
        _keyboard?.Dispose();
        _keyboard = null;
#endif
        base.OnDisappearing();
    }

    private async void OnSortClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheetAsync("Sort results by", "Cancel", null, "Address", "Name", "MAC address");
        var sort = choice switch { "Name" => SearchSort.Name, "MAC address" => SearchSort.Mac, "Address" => SearchSort.Address, _ => (SearchSort?)null };
        if (sort is { } chosen)
        {
            _viewModel.ChooseSortCommand.Execute(chosen);
        }
    }

    private void AnnounceResult()
    {
        var text = _viewModel.HasError ? _viewModel.ErrorMessage
            : _viewModel.IsNotFound ? _viewModel.NotFoundTitle
            : _viewModel.IsSingle ? "Found " + _viewModel.Endpoint?.Title
            : _viewModel.ListHeading;
        if (!string.IsNullOrEmpty(text))
        {
            SemanticScreenReader.Default.Announce(text);
        }

#if IOS
        ShowGlass();
#endif
    }

    private void PlaceControls()
    {
        if (!_active || !ActivePage.IsCurrent(this)) return;
        var clear = CoveredEdges.ForScrolling(this);
        var left = 16 + clear.Left;
        var right = 16 + clear.Right;
        Header.Margin = new Thickness(left, 0, right, 0);
        SearchField.Margin = new Thickness(left, 2, right, 0);
        TopFade.Margin = new Thickness(left, 0, right, 0);
        var availableWidth = Root.Width - left - right;
        var twoColumns = availableWidth >= 680;
        if (twoColumns)
        {
            EndpointCards.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
            EndpointCards.RowDefinitions = [new(GridLength.Auto)];
            Grid.SetRow(SecondaryColumn, 0);
            Grid.SetColumn(SecondaryColumn, 1);
        }
        else
        {
            EndpointCards.ColumnDefinitions = [new(GridLength.Star)];
            EndpointCards.RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)];
            Grid.SetRow(SecondaryColumn, 1);
            Grid.SetColumn(SecondaryColumn, 0);
        }
#if IOS
        ChipStripPlacement.Apply(this, ChipScroll, Root, topMargin: 10);
        var (footer, fade) = HelpGlass.Foot(this, clear);
        var keyboard = BodyHost.Handler?.PlatformView is UIKit.UIView host
            ? SectionListPlacement.KeyboardViewportMargin(host.Bounds.Height, _keyboard?.TopIn(host)) : 0;
        PullRefresh.Margin = new Thickness(0, 0, 0, keyboard);
        Body.Padding = new Thickness(left, 20, right, 0);
        Foot.HeightRequest = keyboard > 0 ? SectionListPlacement.FooterGap : footer;
        BottomFade.SetCoveredHeight(keyboard > 0 ? 0 : fade);
#else
        Chips.Padding = new Thickness(left, 0, right, 0);
        Body.Padding = new Thickness(left, 20, right, 0);
        Foot.HeightRequest = 24 + clear.Bottom;
#endif
    }

    /// <summary>
    /// Where a device's MAC was learned in the "Connected to" card: shows the switch
    /// status, model/IP, badges (kind, switch state, VLAN, MAC count, seen age) and
    /// the port's description, status, speed and traffic, matching Desktop's card.
    /// </summary>
    private static DataTemplate ConnectedLocationTemplate() => new(() =>
    {
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12, Padding = new Thickness(14, 11, 6, 11), MinimumHeightRequest = 56 };
        var line = new BoxView { Style = (Style)Application.Current!.Resources["RowLine"], VerticalOptions = LayoutOptions.Start, Margin = new Thickness(-14, -11, -6, 0) };
        AutomationProperties.SetIsInAccessibleTree(line, false);
        grid.Add(line, 0, 0);
        Grid.SetColumnSpan(line, 3);

        var dot = new Microsoft.Maui.Controls.Shapes.Ellipse { Style = (Style)Application.Current!.Resources["Dot"], VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 6, 0, 0) };
        dot.SetBinding(Microsoft.Maui.Controls.Shapes.Shape.FillProperty, new Binding("Switch.Severity", converter: (IValueConverter)Application.Current!.Resources["StatusColor"]));
        AutomationProperties.SetIsInAccessibleTree(dot, false);
        grid.Add(dot, 0, 0);

        var title = new Label { Style = (Style)Application.Current!.Resources["RowTitle"], LineBreakMode = LineBreakMode.WordWrap };
        title.SetBinding(Label.TextProperty, nameof(LocationItemViewModel.DeviceName));

        var switchDesc = new Label { Style = (Style)Application.Current!.Resources["Caption"], LineBreakMode = LineBreakMode.WordWrap };
        switchDesc.SetBinding(Label.TextProperty, "Switch.Description");
        switchDesc.SetBinding(IsVisibleProperty, "Switch.HasDescription");

        var meta = new Label { Style = (Style)Application.Current!.Resources["Faint"], LineBreakMode = LineBreakMode.WordWrap };
        meta.SetBinding(Label.TextProperty, new MultiBinding
        {
            Bindings = { new Binding(nameof(LocationItemViewModel.KindText)), new Binding("Switch.StateText"), new Binding(nameof(LocationItemViewModel.FactsText)) },
            StringFormat = "{0} · {1} · {2}",
        });

        var portLine = new Label { FontFamily = "Mono", FontSize = 12.5, LineBreakMode = LineBreakMode.CharacterWrap };
        portLine.SetBinding(Label.TextProperty, nameof(LocationItemViewModel.PortName));

        var portState = new Label { Style = (Style)Application.Current!.Resources["Faint"], LineBreakMode = LineBreakMode.WordWrap };
        portState.SetBinding(Label.TextProperty, nameof(LocationItemViewModel.PortStateText));
        portState.SetBinding(IsVisibleProperty, nameof(LocationItemViewModel.HasPort));

        var text = new VerticalStackLayout { Spacing = 3, InputTransparent = true, Children = { title, switchDesc, meta, portLine, portState } };
        AutomationProperties.SetExcludedWithChildren(text, true);
        grid.Add(text, 1, 0);

        var open = new Button { Text = string.Empty, BackgroundColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 0, Padding = 0, Margin = new Thickness(-14, -11, 0, -11), MinimumHeightRequest = 56 };
        open.SetBinding(Button.CommandProperty, nameof(LocationItemViewModel.OpenDeviceCommand));
        open.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(LocationItemViewModel.AccessibleText), stringFormat: "{0}. Opens the switch"));
        grid.Add(open, 0, 0);
        Grid.SetColumnSpan(open, 2);

        var graphs = new IconButton { Kind = IconButtonKind.Plain, Icon = Icons.Graph };
        graphs.SetBinding(IconButton.CommandProperty, nameof(LocationItemViewModel.OpenPortCommand));
        graphs.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(LocationItemViewModel.PortName), stringFormat: "Open graphs for {0}"));
        grid.Add(graphs, 2, 0);
        return grid;
    });

    /// <summary>A switch port that learned the endpoint's MAC: the switch, the port, and whether it's where it plugs in.</summary>
    private static DataTemplate LocationTemplate() => new(() =>
    {
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12, Padding = new Thickness(14, 11, 6, 11), MinimumHeightRequest = 56 };
        var line = new BoxView { Style = (Style)Application.Current!.Resources["RowLine"], VerticalOptions = LayoutOptions.Start, Margin = new Thickness(-14, -11, -6, 0) };
        AutomationProperties.SetIsInAccessibleTree(line, false);
        grid.Add(line, 0, 0);
        Grid.SetColumnSpan(line, 3);

        var dot = new Microsoft.Maui.Controls.Shapes.Ellipse { Style = (Style)Application.Current!.Resources["Dot"], VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 6, 0, 0) };
        dot.SetBinding(Microsoft.Maui.Controls.Shapes.Shape.FillProperty, new Binding(nameof(LocationItemViewModel.KindTone), converter: (IValueConverter)Application.Current!.Resources["StatusColor"]));
        AutomationProperties.SetIsInAccessibleTree(dot, false);
        grid.Add(dot, 0, 0);

        var title = new Label { Style = (Style)Application.Current!.Resources["RowTitle"], LineBreakMode = LineBreakMode.WordWrap };
        title.SetBinding(Label.TextProperty, nameof(LocationItemViewModel.DeviceName));
        var port = new Label { FontFamily = "Mono", FontSize = 12.5, LineBreakMode = LineBreakMode.CharacterWrap };
        port.SetBinding(Label.TextProperty, nameof(LocationItemViewModel.PortName));
        var kind = new Label { Style = (Style)Application.Current!.Resources["Caption"], LineBreakMode = LineBreakMode.WordWrap };
        kind.SetBinding(Label.TextProperty, new MultiBinding
        {
            Bindings = { new Binding(nameof(LocationItemViewModel.KindText)), new Binding(nameof(LocationItemViewModel.FactsText)) },
            StringFormat = "{0} · {1}",
        });
        var state = new Label { Style = (Style)Application.Current!.Resources["Faint"] };
        state.SetBinding(Label.TextProperty, nameof(LocationItemViewModel.PortStateText));
        state.SetBinding(IsVisibleProperty, nameof(LocationItemViewModel.HasPort));
        var text = new VerticalStackLayout { Spacing = 2, InputTransparent = true, Children = { title, port, kind, state } };
        AutomationProperties.SetExcludedWithChildren(text, true);
        grid.Add(text, 1, 0);

        var open = new Button { Text = string.Empty, BackgroundColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 0, Padding = 0, Margin = new Thickness(-14, -11, 0, -11), MinimumHeightRequest = 56 };
        open.SetBinding(Button.CommandProperty, nameof(LocationItemViewModel.OpenDeviceCommand));
        open.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(LocationItemViewModel.AccessibleText), stringFormat: "{0}. Opens the switch"));
        grid.Add(open, 0, 0);
        Grid.SetColumnSpan(open, 2);

        var graphs = new IconButton { Kind = IconButtonKind.Plain, Icon = Icons.Graph };
        graphs.SetBinding(IconButton.CommandProperty, nameof(LocationItemViewModel.OpenPortCommand));
        graphs.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(LocationItemViewModel.PortName), stringFormat: "Open graphs for {0}"));
        grid.Add(graphs, 2, 0);
        return grid;
    });

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void StopInsets()
    {
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif
}
