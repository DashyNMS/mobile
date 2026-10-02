using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// A short rolling log the user can share from Settings (#107): the app's
/// own log messages, plus what the app was doing - pages appearing, the app
/// going to the background and coming back - so a report like "a page went
/// blank" comes with what led up to it.
/// </summary>
/// <remarks>
/// <para>Kept in a file as well as memory, since the usual cure for a stuck
/// app is restarting it, which would otherwise lose the evidence. The last
/// <see cref="MaxLines"/> lines are kept; it never leaves the phone unless the
/// user shares it.</para>
/// <para>Information and above from the app and Core, warnings and above
/// from anything else (MAUI, HTTP) - enough to follow, not a firehose.
/// Nothing here logs tokens or passwords; it can name the server and devices,
/// which Settings says before sharing.</para>
/// </remarks>
public sealed class DiagnosticsLog : ILoggerProvider
{
    public const int MaxLines = 1000;

    private readonly object _gate = new();
    private readonly LinkedList<string> _lines = new();
    private readonly string? _path;
    private readonly TimeProvider _time;
    private int _fileLines;

    public DiagnosticsLog(string? path, TimeProvider? time = null, string? appVersion = null)
    {
        _path = path;
        _time = time ?? TimeProvider.System;
        Load();
        Note("App", $"Started{(appVersion is null ? string.Empty : $" - DashyNMS mobile {appVersion}")}");
    }

    /// <summary>The lines kept, oldest first.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return _lines.ToList();
            }
        }
    }

    /// <summary>Everything kept, as the shared file's text.</summary>
    public string Text => string.Join("\n", Lines) + "\n";

    /// <summary>Something the app did: "Alerts appeared", "Resumed".</summary>
    public void Note(string category, string message) => Add($"{Stamp()} {category}: {message}");

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private string Stamp() => _time.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private void Add(string line)
    {
        // One line each, so the file reads (and trims) line by line.
        line = line.Replace("\r", " ").Replace("\n", " | ");
        lock (_gate)
        {
            _lines.AddLast(line);
            while (_lines.Count > MaxLines)
            {
                _lines.RemoveFirst();
            }

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
                    File.WriteAllLines(_path, _lines);
                    _fileLines = _lines.Count;
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
                    _lines.AddLast(line);
                }

                File.WriteAllLines(_path, _lines);
                _fileLines = _lines.Count;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

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
            log.Add($"{log.Stamp()} {_shortName}{level}: {formatter(state, exception)}{error}");
        }
    }
}
