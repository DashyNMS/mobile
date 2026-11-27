using System.ComponentModel;
using DashyNMS.Mobile.Logs;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Rising filter sheet for <c>LogsPage</c> (#718): keeps Device, Type/State, Time range,
/// Auto-refresh and Clear filters reachable as full-width 48pt rows when the narrow list pane
/// collapses the horizontal chip strip to a labelled Filters button.
/// </summary>
public sealed class LogFilterSheet : Border
{
    private readonly ScrollView _scroller;
    private readonly VerticalStackLayout _rows;
    private readonly SheetRow _deviceRow;
    private readonly BoxView _deviceDivider;
    private readonly SheetRow _typeRow;
    private readonly BoxView _typeDivider;
    private readonly SheetRow _stateRow;
    private readonly BoxView _stateDivider;
    private readonly SheetRow _rangeRow;
    private readonly BoxView _rangeDivider;
    private readonly SheetRow _autoRefreshRow;
    private readonly BoxView _intervalDivider;
    private readonly SheetRow _intervalRow;
    private readonly Button _clearButton;
    private LogsViewModel? _viewModel;

    public LogFilterSheet()
    {
        SafeAreaEdges = SafeAreaEdges.None;
        StrokeShape = new RoundRectangle { CornerRadius = 16 };
        StrokeThickness = 1;
        Padding = new Thickness(14);
        IsVisible = false;
        this.SetAppThemeColor(BackgroundColorProperty, StatusTheme.Color("SurfaceLight"), StatusTheme.Color("SurfaceDark"));
        this.SetAppThemeColor(StrokeProperty, StatusTheme.Color("StrokeLight"), StatusTheme.Color("StrokeDark"));

        var title = new Label
        {
            Text = "Filters",
            FontFamily = "BodySemibold",
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center,
        };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);

        var done = new Button
        {
            Text = "Done",
            BackgroundColor = Colors.Transparent,
            FontFamily = "BodySemibold",
            FontSize = 14,
            Padding = new Thickness(8, 0),
            MinimumHeightRequest = 44,
            MinimumWidthRequest = 44,
            VerticalOptions = LayoutOptions.Center,
        };
        done.SetAppThemeColor(Button.TextColorProperty, StatusTheme.Color("PrimaryTextLight"), StatusTheme.Color("PrimaryTextDark"));
        done.Clicked += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        var header = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            SafeAreaEdges = SafeAreaEdges.None,
        };
        header.Add(title, 0, 0);
        header.Add(done, 1, 0);

        _rows = new VerticalStackLayout
        {
            Spacing = 0,
            SafeAreaEdges = SafeAreaEdges.None,
        };

        _deviceRow = CreateRow("Device", hasChevron: true, () =>
        {
            if (_viewModel is { IsDeviceFixed: false })
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                _viewModel.ChooseDeviceCommand.Execute(null);
            }
        });
        _deviceDivider = CreateDivider();

        _typeRow = CreateRow("Type", hasChevron: true, () => _viewModel?.ChooseTypeCommand.Execute(null));
        _typeDivider = CreateDivider();

        _stateRow = CreateRow("State", hasChevron: true, () => _viewModel?.ChooseStateCommand.Execute(null));
        _stateDivider = CreateDivider();

        _rangeRow = CreateRow("Time range", hasChevron: true, () => _viewModel?.ChooseRangeCommand.Execute(null));
        _rangeDivider = CreateDivider();

        _autoRefreshRow = CreateRow("Auto-refresh", hasChevron: false, () => _viewModel?.ToggleAutoRefreshCommand.Execute(null));
        _intervalDivider = CreateDivider();

        _intervalRow = CreateRow("Refresh interval", hasChevron: true, () => _viewModel?.ChooseAutoRefreshIntervalCommand.Execute(null));

        _clearButton = new Button
        {
            Text = "Clear filters",
            Margin = new Thickness(0, 10, 0, 2),
            MinimumHeightRequest = 44,
            IsVisible = false,
        };
        if (Application.Current?.Resources.TryGetValue("SecondaryButton", out var secondary) == true && secondary is Style style)
        {
            _clearButton.Style = style;
        }
        _clearButton.Clicked += (_, _) => _viewModel?.ClearFiltersCommand.Execute(null);

        _rows.Add(_deviceRow.Container);
        _rows.Add(_deviceDivider);
        _rows.Add(_typeRow.Container);
        _rows.Add(_typeDivider);
        _rows.Add(_stateRow.Container);
        _rows.Add(_stateDivider);
        _rows.Add(_rangeRow.Container);
        _rows.Add(_rangeDivider);
        _rows.Add(_autoRefreshRow.Container);
        _rows.Add(_intervalDivider);
        _rows.Add(_intervalRow.Container);
        _rows.Add(_clearButton);

        _scroller = new ScrollView
        {
            Content = _rows,
            SafeAreaEdges = SafeAreaEdges.None,
        };

        var layout = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            RowSpacing = 4,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        layout.Add(header, 0, 0);
        layout.Add(_scroller, 0, 1);
        Content = layout;
    }

    /// <summary>Raised when the user taps Done or navigates away to the device picker.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Connects the sheet rows to <paramref name="viewModel"/> so live updates reflect immediately.</summary>
    public void Attach(LogsViewModel viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            Refresh();
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.AutoRefresh.PropertyChanged -= OnAutoRefreshPropertyChanged;
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.AutoRefresh.PropertyChanged += OnAutoRefreshPropertyChanged;
        Refresh();
    }

    /// <summary>Caps the sheet body height so it stays scrollable on short windows or large Dynamic Type.</summary>
    public void UpdateMaxHeight(double viewportHeight)
    {
        if (viewportHeight <= 0 || !double.IsFinite(viewportHeight))
        {
            return;
        }

        var maxBody = Math.Max(140, (viewportHeight * 0.56) - 72);
        if (Math.Abs(_scroller.MaximumHeightRequest - maxBody) > 1)
        {
            _scroller.MaximumHeightRequest = maxBody;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or ""
            or nameof(LogsViewModel.DeviceChipText)
            or nameof(LogsViewModel.CanClearDevice)
            or nameof(LogsViewModel.IsDeviceFixed)
            or nameof(LogsViewModel.ShowingEventLog)
            or nameof(LogsViewModel.ShowingAlertLog)
            or nameof(LogsViewModel.TypeChipText)
            or nameof(LogsViewModel.IsTypeSet)
            or nameof(LogsViewModel.StateChipText)
            or nameof(LogsViewModel.IsStateSet)
            or nameof(LogsViewModel.RangeChipText)
            or nameof(LogsViewModel.IsRangeSet)
            or nameof(LogsViewModel.HasActiveFilters))
        {
            Refresh();
        }
    }
    private void OnAutoRefreshPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    /// <summary>Updates all row labels, values, visibility and accessible descriptions from the view model.</summary>
    public void Refresh()
    {
        if (_viewModel is null)
        {
            return;
        }

        var vm = _viewModel;
        var fixedDevice = vm.IsDeviceFixed;
        _deviceRow.Value.Text = vm.DeviceChipText;
        _deviceRow.Chevron!.IsVisible = !fixedDevice;
        _deviceRow.Button.IsEnabled = !fixedDevice;
        SetValueAccent(_deviceRow.Value, vm.CanClearDevice);
        SemanticProperties.SetDescription(_deviceRow.Button, fixedDevice ? $"Device, {vm.DeviceChipText}" : $"Choose device, {vm.DeviceChipText}");

        var showEvent = vm.ShowingEventLog;
        _typeRow.Container.IsVisible = showEvent;
        _typeDivider.IsVisible = showEvent;
        _typeRow.Value.Text = vm.TypeChipText;
        SetValueAccent(_typeRow.Value, vm.IsTypeSet);
        SemanticProperties.SetDescription(_typeRow.Button, $"Type, {vm.TypeChipText}");

        var showAlert = vm.ShowingAlertLog;
        _stateRow.Container.IsVisible = showAlert;
        _stateDivider.IsVisible = showAlert;
        _stateRow.Value.Text = vm.StateChipText;
        SetValueAccent(_stateRow.Value, vm.IsStateSet);
        SemanticProperties.SetDescription(_stateRow.Button, $"State, {vm.StateChipText}");

        _rangeRow.Value.Text = vm.RangeChipText;
        SetValueAccent(_rangeRow.Value, vm.IsRangeSet);
        SemanticProperties.SetDescription(_rangeRow.Button, $"Time range, {vm.RangeChipText}");

        var autoOn = vm.AutoRefresh.IsOn;
        _autoRefreshRow.Value.Text = autoOn ? "On" : "Off";
        SetValueAccent(_autoRefreshRow.Value, autoOn);
        SemanticProperties.SetDescription(_autoRefreshRow.Button, $"Auto-refresh, {(autoOn ? "On" : "Off")}");

        _intervalDivider.IsVisible = autoOn;
        _intervalRow.Container.IsVisible = autoOn;
        _intervalRow.Value.Text = vm.AutoRefresh.IntervalText;
        SetValueAccent(_intervalRow.Value, autoOn);
        SemanticProperties.SetDescription(_intervalRow.Button, $"Refresh interval, {vm.AutoRefresh.IntervalText}");

        _clearButton.IsVisible = vm.HasActiveFilters;
    }

    private static void SetValueAccent(Label label, bool active)
    {
        if (active)
        {
            label.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("PrimaryTextLight"), StatusTheme.Color("PrimaryTextDark"));
            label.FontFamily = "BodySemibold";
        }
        else
        {
            label.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
            label.FontFamily = "BodyRegular";
        }
    }

    private static BoxView CreateDivider()
    {
        var divider = new BoxView { HeightRequest = 1 };
        divider.SetAppThemeColor(BoxView.ColorProperty, StatusTheme.Color("StrokeLight"), StatusTheme.Color("StrokeDark"));
        AutomationProperties.SetIsInAccessibleTree(divider, false);
        return divider;
    }

    private static SheetRow CreateRow(string labelText, bool hasChevron, Action onClick)
    {
        var label = new Label
        {
            Text = labelText,
            FontFamily = "BodySemibold",
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        label.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextLight"), StatusTheme.Color("TextDark"));
        AutomationProperties.SetIsInAccessibleTree(label, false);

        var value = new Label
        {
            FontFamily = "BodyRegular",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        value.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        AutomationProperties.SetIsInAccessibleTree(value, false);

        Microsoft.Maui.Controls.Shapes.Path? chevron = null;
        if (hasChevron)
        {
            chevron = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M9 6 15 12 9 18")!,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                WidthRequest = 16,
                HeightRequest = 16,
                Aspect = Stretch.Uniform,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
            };
            chevron.SetAppThemeColor(Shape.StrokeProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
            AutomationProperties.SetIsInAccessibleTree(chevron, false);
        }

        var visual = new Grid
        {
            ColumnDefinitions = hasChevron
                ? [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
                : [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 12,
            Padding = new Thickness(4, 10),
            MinimumHeightRequest = 48,
            InputTransparent = true,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        StatusFilterBar.ExcludeVisualFromAccessibility(visual);
        visual.Add(label, 0, 0);
        visual.Add(value, 1, 0);
        if (chevron is not null)
        {
            visual.Add(chevron, 2, 0);
        }

        var button = StatusFilterBar.CreateOverlayButton(cornerRadius: 10, minHeight: 48);
        button.Clicked += (_, _) => onClick();
        StatusFilterBar.AttachPressFeedback(button, visual);

        var container = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        container.Add(visual);
        container.Add(button);

        return new SheetRow(container, value, chevron, button);
    }

    private sealed class SheetRow(
        Grid container,
        Label value,
        Microsoft.Maui.Controls.Shapes.Path? chevron,
        Button button)
    {
        public Grid Container { get; } = container;
        public Label Value { get; } = value;
        public Microsoft.Maui.Controls.Shapes.Path? Chevron { get; } = chevron;
        public Button Button { get; } = button;
    }
}
