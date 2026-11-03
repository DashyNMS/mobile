using DesktopNMS.Core.Alerting;

namespace DashyNMS.Mobile.Unimus;

/// <summary>Diff-only identity around the shared contents line; source text is never rewritten.</summary>
public sealed class UnimusDiffItem(UnimusDiffRow row)
{
    private readonly UnimusLine _line = new(row);
    public UnimusDiffRow Row => _line.Row;
    public string NumberText => _line.NumberText;
    public string Prefix => _line.Prefix;
    public string Text => _line.Text;
    public bool IsAdded => _line.IsAdded;
    public bool IsRemoved => _line.IsRemoved;
    public bool IsHidden => _line.IsHidden;
    public bool IsContent => !IsHidden;
    public string ContentDescription => IsHidden ? Text
        : $"{(IsAdded ? "Added" : IsRemoved ? "Removed" : "Context")}, original line {Row.OldNumber?.ToString() ?? "none"}, revised line {Row.NewNumber?.ToString() ?? "none"}: {Text}";
}
