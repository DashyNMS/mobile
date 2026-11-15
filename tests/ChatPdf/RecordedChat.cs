using DashyNMS.Mobile.Intelligence;

internal static class RecordedChat
{
    public static ExportedChat Create(byte[] graph)
    {
        var answer = "# Recorded findings\n\n| Port | State |\n| --- | --- |\n| Ethernet1 | Up |\n\n```\nshow interface Ethernet1\n```\n\n[Reference](https://example.net/source)\n\n"
            + string.Join("\n\n", Enumerable.Range(0, 90).Select(i => $"Record{i:D3}: Synthetic network evidence retained in the exported conversation. Every paragraph must survive pagination and remain selectable."));
        return new("Recorded export", DateTimeOffset.UnixEpoch,
            [new(true, null, "Show all recorded network evidence.", [], null),
             new(false, null, answer, [new(graph, "image/png")], "This answer was interrupted before it finished.")]);
    }
}
