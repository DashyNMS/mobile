namespace DashyNMS.Mobile.Help;

/// <summary>
/// A fenced code block's opening line, as CommonMark reads it: three or more
/// backticks or tildes. Only a line of the same character, at least as long and
/// with nothing after it, closes it, so a shorter fence, the other character or
/// a fence with a language after it stays inside the code.
/// </summary>
internal readonly record struct MarkdownFence(char Marker, int Length, string Indent = "")
{
    /// <param name="nested">For display, which has no nested blocks: code indented under a list item still counts.</param>
    public static bool TryOpen(string line, out MarkdownFence fence, bool nested = false)
    {
        fence = default;
        if (Unindented(line, nested) is not { } text) return false;
        if (text.Length < 3 || text[0] is not ('`' or '~')) return false;
        var length = 0;
        while (length < text.Length && text[length] == text[0]) length++;

        // A backtick fence's language can't contain a backtick: that's inline code.
        if (length < 3 || (text[0] == '`' && text.AsSpan(length).Contains('`'))) return false;
        fence = new MarkdownFence(text[0], length, line[..(line.Length - line.TrimStart().Length)]);
        return true;
    }

    public bool IsClosedBy(string line, bool nested = false)
    {
        if (Unindented(line, nested) is not { } unindented) return false;
        var text = unindented.TrimEnd();
        var marker = Marker;
        return text.Length >= Length && text.All(c => c == marker);
    }

    /// <summary>The line without its indent, or null when indented four or more spaces (or a tab): CommonMark reads that as code, not a fence.</summary>
    private static string? Unindented(string line, bool nested)
    {
        if (nested) return line.TrimStart();
        var spaces = 0;
        while (spaces < line.Length && line[spaces] == ' ') spaces++;
        return spaces <= 3 && (spaces == line.Length || line[spaces] != '\t') ? line[spaces..] : null;
    }
}
