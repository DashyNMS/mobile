using System.Globalization;
using DesktopNMS.Core.Alerting;

namespace DashyNMS.Mobile.Unimus;

/// <summary>
/// One line of a backup or a diff, for the monospaced viewer: its number, a
/// "+" or "-" so a change doesn't rely on colour alone, and its text.
/// </summary>
public sealed class UnimusLine
{
    public UnimusLine(UnimusDiffRow row) => Row = row;

    public UnimusDiffRow Row { get; }

    /// <summary>One number on a phone, not desktop's two columns: the new side's, or the old side's for a removed line.</summary>
    public string NumberText => (Row.NewNumber ?? Row.OldNumber)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string Prefix => Row.Kind switch
    {
        UnimusDiffRowKind.Removed => "-",
        UnimusDiffRowKind.Added => "+",
        _ => " ",
    };

    public string Text => IsHidden
        ? $"… {Row.HiddenCount} unchanged {(Row.HiddenCount == 1 ? "line" : "lines")}, tap to show"
        : Row.Text;

    public bool IsAdded => Row.Kind == UnimusDiffRowKind.Added;

    public bool IsRemoved => Row.Kind == UnimusDiffRowKind.Removed;

    public bool IsHidden => Row.Kind == UnimusDiffRowKind.Hidden;

    /// <summary>A backup's text as numbered lines.</summary>
    public static IReadOnlyList<UnimusLine> FromContent(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select((text, i) => new UnimusLine(new UnimusDiffRow(UnimusDiffRowKind.Common, null, i + 1, text)))
            .ToList();
}
