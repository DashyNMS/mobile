using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// A short rolling log the user can share from Settings (#107, #126): the
/// app's own log messages and what the app was doing - requests to
/// LibreNMS, alert checks, notifications, what was opened, slow loads,
/// errors, the system - so a report like "a page went blank" or "it's slow"
/// comes with what led up to it.
/// </summary>
/// <remarks>
/// <para>Kept in a file as well as memory, since the usual cure for a stuck
/// app is restarting it, which would otherwise lose the evidence. The last
/// <see cref="MaxLines"/> lines are kept - the ordinary ones go first, so
/// errors, warnings and slow loads outlast the routine. It never leaves the
/// phone unless the user shares it.</para>
/// <para>Less noise (#126): pages opened one after another make one trail
/// ("Pages: Alerts → Devices → Dashboard"), and the same line again makes
/// the last one "× 2", "× 3".</para>
/// <para>Information and above from the app and Core, warnings and above
/// from anything else (MAUI, HTTP) - enough to follow, not a firehose.
/// Nothing here logs tokens or passwords; it can name the server and devices,
/// which Settings says before sharing, and can hide on the way out.</para>
/// </remarks>
public sealed class DiagnosticsLog : ILoggerProvider
{
    public const int MaxLines = 1000;

    /// <summary>A trail of pages longer ago than this starts a new line.</summary>
    internal static readonly TimeSpan TrailGap = TimeSpan.FromMinutes(2);

    private const string PagesCategory = "Pages";

    private readonly object _gate = new();
    private readonly LinkedList<Entry> _lines = new();
    private readonly string? _path;
    private readonly TimeProvider _time;
    private int _fileLines;

    public DiagnosticsLog(string? path, TimeProvider? time = null, string? appVersion = null, string? device = null)
    {
        _path = path;
        _time = time ?? TimeProvider.System;
        AppVersion = appVersion;
        Device = device;
        Load();
        Note("App", $"Started{(appVersion is null ? string.Empty : $" - DashyNMS mobile {appVersion}")}{(device is null ? string.Empty : $" on {device}")}");
    }

    /// <summary>"1.1.0 (162)" - for the shared file's header.</summary>
    public string? AppVersion { get; }

    /// <summary>"Apple iPhone16,1 · iOS 26.1" - for the shared file's header.</summary>
    public string? Device { get; }

    /// <summary>The lines kept, oldest first.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return _lines.Select(e => e.Text).ToList();
            }
        }
    }

    /// <summary>Everything kept, as the shared file's text.</summary>
    public string Text => string.Join("\n", Lines) + "\n";

    /// <summary>The file to share: <paramref name="header"/>, then every line - names hidden if asked (#126).</summary>
    public string Report(IEnumerable<string> header, DiagnosticsRedaction? redaction = null)
    {
        var text = string.Join("\n", header) + "\n\n" + Text;
        return redaction is null ? text : redaction.Apply(text);
    }

    /// <summary>Something the app did: "Resumed", "Alert check: 3 changes".</summary>
    public void Note(string category, string message) => Add(category, message, important: false);

    /// <summary>Worth keeping when the log is trimmed: a slow load, a failure.</summary>
    public void NoteImportant(string category, string message) => Add(category, message, important: true);

    /// <summary>
    /// A page shown: added to the trail of pages just opened, rather than a
    /// line each - "Pages: Alerts → Devices → Dashboard".
    /// </summary>
    public void NotePage(string page)
    {
        var now = _time.GetLocalNow();
        lock (_gate)
        {
            if (_lines.Last is { Value: { Category: PagesCategory } last } node && now - last.At < TrailGap)
            {
                if (!last.Text.EndsWith("→ " + page, StringComparison.Ordinal) && !last.Text.EndsWith(": " + page, StringComparison.Ordinal))
                {
                    node.Value = last with { Text = last.Text + " → " + page, Message = last.Message + " → " + page, At = now };
                    RewriteFile();
                }

                return;
            }
        }

        Add(PagesCategory, page, important: false);
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private string Stamp(DateTimeOffset at) => at.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private void Add(string category, string message, bool important)
    {
        // One line each, so the file reads (and trims) line by line.
        message = message.Replace("\r", " ").Replace("\n", " | ");
        var now = _time.GetLocalNow();

        lock (_gate)
        {
            // The same again: the last line counts it rather than repeating it -
            // except a new trail of pages, which is a trail, not a repeat.
            if (category != PagesCategory
                && _lines.Last is { Value: { } last } lastNode && last.Category == category && last.Message == message)
            {
                var count = last.Count + 1;
                lastNode.Value = last with { Count = count, At = now, Text = $"{Stamp(last.Started)} {category}: {message} × {count}" };
                RewriteFile();
                return;
            }

            var entry = new Entry(category, message, $"{Stamp(now)} {category}: {message}", important, now, now, 1);
            _lines.AddLast(entry);
            Trim();
            Append(entry.Text);
        }
    }

    /// <summary>Over the limit: the oldest ordinary line goes first, so the rarer, more useful ones last longest.</summary>
    private void Trim()
    {
        while (_lines.Count > MaxLines)
        {
            var node = _lines.First;
            while (node is not null && node.Value.Important)
            {
                node = node.Next;
            }

            _lines.Remove(node ?? _lines.First!);
        }
    }

    private void Append(string line)
    {
        try
        {
            if (_path is null)
            {
                return;
            }

            // Each line a cheap append; the file is cut back to what's kept
            // only once it has grown to twice that.
            File.AppendAllText(_path, line + "\n");
            if (++_fileLines > MaxLines * 2)
            {
                RewriteFile();
            }
        }
        catch (IOException)
        {
            // Diagnostics must never break the app; memory still has it.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The file as kept - after a line changed in place (a count, a trail) or it grew too long.</summary>
    private void RewriteFile()
    {
        try
        {
            if (_path is null)
            {
                return;
            }

            File.WriteAllLines(_path, _lines.Select(e => e.Text));
            _fileLines = _lines.Count;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Load()
    {
        if (_path is null || !File.Exists(_path))
        {
            return;
        }

        try
        {
            var previous = File.ReadAllLines(_path);
            lock (_gate)
            {
                foreach (var line in previous.Skip(Math.Max(0, previous.Length - MaxLines)))
                {
                    _lines.AddLast(new Entry(string.Empty, line, line, IsImportant(line), DateTimeOffset.MinValue, DateTimeOffset.MinValue, 1));
                }

                RewriteFile();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A line from an earlier run, read back: its weight is in its words.</summary>
    private static bool IsImportant(string line) =>
        line.Contains("[warning]", StringComparison.Ordinal)
        || line.Contains("[error]", StringComparison.Ordinal)
        || line.Contains("Slow load", StringComparison.Ordinal);

    /// <param name="Started">When a run of the same line began - its stamp.</param>
    private sealed record Entry(string Category, string Message, string Text, bool Important, DateTimeOffset At, DateTimeOffset Started, int Count);

    private sealed class Logger(DiagnosticsLog log, string category) : ILogger
    {
        private readonly string _shortName = category[(category.LastIndexOf('.') + 1)..];

        private readonly bool _isOurs = category.StartsWith("DashyNMS", StringComparison.Ordinal)
            || category.StartsWith("DesktopNMS", StringComparison.Ordinal);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= (_isOurs ? LogLevel.Information : LogLevel.Warning) && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var level = logLevel switch
            {
                LogLevel.Warning => " [warning]",
                LogLevel.Error or LogLevel.Critical => " [error]",
                _ => string.Empty,
            };
            var error = exception is null ? string.Empty : $" - {exception.GetType().Name}: {exception.Message}";
            log.Add(_shortName + level, formatter(state, exception) + error, important: logLevel >= LogLevel.Warning);
        }
    }
}
