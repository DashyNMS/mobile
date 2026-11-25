namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Full-width setup slots: a fixed header, a finite viewport and an optional
/// fixed action. The viewport owns its scrolling; lists must not be wrapped in a ScrollView.
/// </summary>
public sealed class SetupLayout : Grid
{
    private View? _header;
    private View? _body;
    private View? _primaryAction;
    private bool _overlayPrimaryAction;

    public SetupLayout()
    {
        RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)];
#if IOS
        SafeAreaEdges = SafeAreaEdges.None;
#endif
    }

    /// <summary>
    /// Opt-in iOS overlay mode: when true on iOS, <see cref="PrimaryAction"/> floats
    /// over row 1 (<see cref="Body"/>) above a <see cref="FootFade"/> so the form body
    /// scrolls full-height behind Save. Android always retains the <c>Auto</c> row 2.
    /// </summary>
    public bool OverlayPrimaryAction
    {
        get => _overlayPrimaryAction;
        set
        {
            if (_overlayPrimaryAction == value) return;
            _overlayPrimaryAction = value;
            SyncPrimaryActionSlot();
        }
    }

    internal FootFade? FootFade { get; private set; }

    public View? Header
    {
        get => _header;
        set => SetSlot(ref _header, 0, value);
    }

    public View? Body
    {
        get => _body;
        set
        {
            SetSlot(ref _body, 1, value);
            EnsureZOrder();
        }
    }

    public View? PrimaryAction
    {
        get => _primaryAction;
        set
        {
            if (_primaryAction is not null) Remove(_primaryAction);
            _primaryAction = value;
            if (value is not null)
            {
                Add(value);
                SyncPrimaryActionSlot();
            }
        }
    }

    /// <summary>Ensures a <see cref="FootFade"/> is hosted on row 1 above <see cref="Body"/> and below <see cref="PrimaryAction"/>.</summary>
    internal FootFade EnsureFootFade()
    {
        if (FootFade is not null) return FootFade;
        FootFade = new FootFade();
        SetRow((IView)FootFade, 1);
        Add(FootFade);
        EnsureZOrder();
        return FootFade;
    }

    private void SyncPrimaryActionSlot()
    {
        if (_primaryAction is null) return;
#if IOS
        if (_overlayPrimaryAction)
        {
            SetRow((IView)_primaryAction, 1);
            _primaryAction.VerticalOptions = LayoutOptions.End;
            EnsureFootFade();
            EnsureZOrder();
            return;
        }
#endif
        SetRow((IView)_primaryAction, 2);
        _primaryAction.VerticalOptions = LayoutOptions.Fill;
        _primaryAction.ZIndex = 0;
        if (FootFade is not null) FootFade.SetCoveredHeight(0);
        EnsureZOrder();
    }

    private void EnsureZOrder()
    {
        if (FootFade is not null) FootFade.ZIndex = 1;
        if (_primaryAction is not null && _overlayPrimaryAction) _primaryAction.ZIndex = 2;
    }

    private void SetSlot(ref View? field, int row, View? value)
    {
        if (field is not null) Remove(field);
        field = value;
        if (value is null) return;
        SetRow((IView)value, row);
        Add(value);
    }
}
