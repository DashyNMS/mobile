using System.Globalization;
using System.Text.Json;

namespace DashyNMS.Mobile.Widgets;

/// <summary>
/// The widgets' lines in Share diagnostics (#286): what the app last saved,
/// whether that failed, and what the widget made of it when it last read it.
/// The widget can't add to the app's log, so it leaves a note of each read
/// beside the snapshot (<see cref="ReadNoteFile"/>) and this reads it back.
/// </summary>
/// <remarks>
/// Counts and times only, never device or rule names, as the rest of the header.
/// </remarks>
public static class WidgetDiagnostics
{
    /// <summary>What the widget writes after each read - its name is a contract with <c>Snapshot.swift</c>.</summary>
    public const string ReadNoteFile = "widget-read.json";

    /// <param name="hasFolder">Whether the App Group folder the app and widget share was found.</param>
    /// <param name="snapshotJson">The saved snapshot, or null when there's none.</param>
    /// <param name="savedAt">When it was saved.</param>
    /// <param name="lastSaveError">Why the last save this session failed, or null.</param>
    /// <param name="readNoteJson">The widget's note of its last read, or null when it hasn't left one.</param>
    public static IReadOnlyList<string> Describe(
        bool hasFolder,
        string? snapshotJson,
        DateTimeOffset? savedAt,
        string? lastSaveError,
        string? readNoteJson,
        DateTimeOffset now)
    {
        if (!hasFolder)
        {
            return ["Widgets: no App Group folder - this build can't share data with the widgets"];
        }

        var lines = new List<string> { "Widgets: " + Saved(snapshotJson, savedAt, now) };
        if (lastSaveError is not null)
        {
            lines.Add("Widgets: last save failed - " + lastSaveError);
        }

        lines.Add("Widgets: " + Read(readNoteJson, now));
        return lines;
    }

    private static string Saved(string? json, DateTimeOffset? savedAt, DateTimeOffset now)
    {
        if (json is null)
        {
            return "no snapshot saved yet";
        }

        var snapshot = WidgetSnapshot.FromJson(json);
        var version = Version(json);
        var when = savedAt is { } at ? $"saved {Ago(at, now)}" : "saved";
        return version != WidgetSnapshot.CurrentVersion
            ? $"{when}, version {version?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} (the widgets read version {WidgetSnapshot.CurrentVersion.ToString(CultureInfo.InvariantCulture)})"
            : snapshot.SignedIn
                ? $"{when} · signed in · {snapshot.AlertGroups.ToString(CultureInfo.InvariantCulture)} alerts · checked {Ago(DateTimeOffset.FromUnixTimeSeconds(snapshot.CheckedAt), now)}"
                : $"{when} · signed out";
    }

    private static string Read(string? note, DateTimeOffset now)
    {
        if (note is null)
        {
            return "the widget hasn't read the snapshot yet";
        }

        try
        {
            using var document = JsonDocument.Parse(note);
            var root = document.RootElement;
            var at = root.TryGetProperty("readAt", out var readAt) && readAt.TryGetInt64(out var seconds)
                ? Ago(DateTimeOffset.FromUnixTimeSeconds(seconds), now)
                : "at an unknown time";
            var outcome = root.TryGetProperty("outcome", out var o) ? o.GetString() : null;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
            var signedIn = root.TryGetProperty("signedIn", out var s) && s.ValueKind == JsonValueKind.True;
            return outcome switch
            {
                "ok" => $"widget read it {at} · {(signedIn ? "signed in" : "signed out")}",
                "no-folder" => $"widget found no App Group folder {at}",
                "no-file" => $"widget found no snapshot {at}",
                _ => $"widget couldn't read it {at}" + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " - " + detail),
            };
        }
        catch (JsonException)
        {
            return "the widget's note of its last read can't be read";
        }
    }

    private static int? Version(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Ago(DateTimeOffset at, DateTimeOffset now) =>
        at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) + " (" + ViewModels.Formatting.Age(now - at) + ")";
}
