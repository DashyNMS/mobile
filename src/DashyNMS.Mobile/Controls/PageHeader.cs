namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A page's big in-page title (#69), as in the mock-ups, with the page's
/// actions beside it. The system navigation bar is never shown (#142): a
/// page that's the root of its tab shows just this, and the same page pushed
/// from More, Devices or Alerts shows "‹ More" above it, as <see cref="BackBar"/>
/// does - so a page looks the same however it was reached.
/// </summary>
/// <remarks>
/// <para>Its first row is the Dashboard's top line (#253): on a tab's own
/// page the title sits on it, centred where "DashyNMS" is, its actions where
/// Customise is; pushed, "‹ More" takes the line and the title goes beneath.
/// So the title never moves between tabs. The page puts no top padding of
/// its own above it - see <see cref="TopBar"/>.</para>
/// <para>Call <see cref="Apply"/> from the page's OnAppearing.</para>
/// <para>It used to hand a pushed page the system bar instead, deciding by the
/// navigation stack's size in OnAppearing. The first time a page is pushed
/// the stack doesn't hold it yet, so it looked like a tab's root and got this
/// header; coming back to it, the stack was right and it switched to the
/// system bar.</para>
/// </remarks>
public sealed class PageHeader : Grid
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (view, _, value) => ((PageHeader)view)._title.Text = value as string);

    /// <summary>A line under the title - "Offline: last updated 07:42" (#130) - hidden when there's nothing to say.</summary>
    public static readonly BindableProperty NoteProperty = BindableProperty.Create(
        nameof(Note), typeof(string), typeof(PageHeader), null,
        propertyChanged: (view, _, value) =>
        {
            var header = (PageHeader)view;
            header._note.Text = value as string;
            header._note.IsVisible = !string.IsNullOrEmpty(value as string);
        });

    private readonly Label _title = new() { Style = (Style)Application.Current!.Resources["PageTitle"] };

    private readonly Label _note = new()
    {
        Style = (Style)Application.Current!.Resources["Faint"],
        IsVisible = false,
        Margin = new Thickness(0, -2, 0, 4),
    };

    private readonly IconButton _back = new()
    {
        Kind = IconButtonKind.Plain,
        Icon = Icons.Back,
        Text = "Back",
        Padding = new Thickness(0, 0, 8, 0),
        HorizontalOptions = LayoutOptions.Start,
        IsVisible = false,
    };

    private readonly BoxView _topStrut = new()
    {
        WidthRequest = 0,
        HeightRequest = TopBar.Height,
        Opacity = 0,
        InputTransparent = true,
        VerticalOptions = LayoutOptions.Center,
    };

    private readonly InlineGlassFallback _fallback = new();
    private WeakReference<ContentPage>? _ownerPage;
#if IOS
    private Action? _fallbackBack;
#endif

    private EventHandler? _titleTapped;

    public PageHeader()
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
        RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
        Padding = new Thickness(0, TopBar.Offset, 0, 4);
        TopBar.Fit(this);

        AutomationProperties.SetIsInAccessibleTree(_topStrut, false);
        Add(_topStrut);
        _back.Command = new Command(OnBackTapped);
        _back.VerticalOptions = LayoutOptions.Center;
        Add(_back);
        Add(_title);
        SetColumn((IView)_fallback, 1);
        Add(_fallback);
        SetRow((IView)_note, 2);
        SetColumnSpan((IView)_note, 2);
        Add(_note);
        SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
#if IOS
        Loaded += (_, _) =>
        {
            TabBarButtons.Moved -= OnGlassMoved;
            TabBarButtons.Moved += OnGlassMoved;
            RefreshFallback();
        };
        Unloaded += (_, _) => TabBarButtons.Moved -= OnGlassMoved;
#endif
    }

    private void OnBackTapped()
    {
#if IOS
        if (_fallbackBack is { } back)
        {
            back();
            return;
        }
#endif
        _ = Shell.Current.GoToAsync("..");
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(RefreshFallback);
#endif

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Note
    {
        get => (string?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>
    /// A tap on the title itself - Settings counts them for its easter egg
    /// (#99). The title only listens once something does, so other pages'
    /// titles stay plain headings for VoiceOver.
    /// </summary>
    public event EventHandler? TitleTapped
    {
        add
        {
            if (_titleTapped is null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => _titleTapped?.Invoke(this, EventArgs.Empty);
                _title.GestureRecognizers.Add(tap);
            }

            _titleTapped += value;
        }

        remove => _titleTapped -= value;
    }

    /// <summary>
    /// Back is glass by the tab bar instead (iOS 26 and later), so a pushed
    /// page shows no "‹ More" of its own and its title takes the top line,
    /// as a tab's own page - the page shows the glass Back itself.
    /// </summary>
    public bool BackInGlass { get; set; }

    /// <summary>The page's actions, at the title's right.</summary>
    public View? Actions
    {
        get => Children.OfType<View>().FirstOrDefault(v => v != _title && v != _back && v != _note && v != _topStrut && v != _fallback);
        set
        {
            if (Actions is { } old)
            {
                Remove(old);
            }

            if (value is not null)
            {
                SetRow((IView)value, GetRow((IView)_title));
                SetColumn((IView)value, 1);
                value.VerticalOptions = LayoutOptions.Center;
                Add(value);
            }
        }
    }

    /// <summary>
    /// Hides the navigation bar, and shows "‹ More" (or wherever it came from)
    /// when <paramref name="page"/> was pushed rather than being its tab's own.
    /// </summary>
    public void Apply(ContentPage page)
    {
        _ownerPage = new WeakReference<ContentPage>(page);
        Shell.SetNavBarIsVisible(page, false);
        IsVisible = true;
        RefreshFallback();
    }

    private void RefreshFallback()
    {
        if (_ownerPage is null || !_ownerPage.TryGetTarget(out var page))
        {
            return;
        }

        var fallbackActive = _fallback.Sync(page);
#if IOS
        var fallback = fallbackActive ? TabBarButtons.GetFallback(page) : default;
        _fallbackBack = fallback.Back?.Tapped;
        var showInlineBack = (IsPushed(page) && !BackInGlass) || (fallback.Active && fallback.Back is not null);
#else
        var showInlineBack = IsPushed(page) && !BackInGlass;
#endif
        _back.IsVisible = showInlineBack;

        // A tab's own page (or Back in the glass): the title on the top line.
        // Pushed (or zero-target fallback with Back): "‹ More" there, the title beneath.
        var row = showInlineBack ? 1 : 0;
        SetRow((IView)_title, row);
        SetRow((IView)_fallback, row);
        if (Actions is { } actions)
        {
            SetRow((IView)actions, row);
        }

        if (showInlineBack)
        {
            _back.Text = BackBar.PreviousTitle(page);
#if IOS
            SemanticProperties.SetDescription(_back, fallback.Back?.Label ?? "Back to " + _back.Text);
#else
            SemanticProperties.SetDescription(_back, "Back to " + _back.Text);
#endif
        }
    }

    /// <summary>
    /// Whether <paramref name="page"/> was pushed: it isn't the page its tab
    /// itself shows. Asked of Shell rather than counted from the navigation
    /// stack, which may not hold the page yet when it first appears.
    /// </summary>
    internal static bool IsPushed(Page page)
    {
        if (Shell.Current?.CurrentItem?.CurrentItem?.CurrentItem is IShellContentController content && content.Page is { } root)
        {
            return !ReferenceEquals(root, page);
        }

        return page.Navigation.NavigationStack.Count > 1;
    }
}
