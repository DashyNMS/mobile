namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Full-width setup slots: a fixed header, a finite viewport and an optional
/// fixed action. The viewport owns its scrolling; lists must not be wrapped in a ScrollView.
/// </summary>
public sealed class SetupLayout : Grid
{
    public SetupLayout()
    {
        RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)];
#if IOS
        SafeAreaEdges = SafeAreaEdges.None;
#endif
    }

    public View? Header { get => GetSlot(0); set => SetSlot(0, value); }
    public View? Body { get => GetSlot(1); set => SetSlot(1, value); }
    public View? PrimaryAction { get => GetSlot(2); set => SetSlot(2, value); }

    private View? GetSlot(int row) => Children.OfType<View>().FirstOrDefault(view => GetRow((IView)view) == row);

    private void SetSlot(int row, View? value)
    {
        if (GetSlot(row) is { } previous) Remove(previous);
        if (value is null) return;
        SetRow((IView)value, row);
        Add(value);
    }
}
