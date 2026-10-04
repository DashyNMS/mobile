using System.Diagnostics;
using System.Globalization;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Every request the app makes - to LibreNMS, Graylog - in the diagnostics
/// (#126): "GET /api/v0/devices · 200 · 412 ms", failures and timeouts as
/// warnings. Read from .NET's own record of each request (the
/// "System.Net.Http" activities), so Core's transport needs no hook of its
/// own; and the slowest of the last few, for a slow page's line.
/// </summary>
/// <remarks>
/// Only the method, host and path go in: the query is dropped and headers
/// are never read, so the API token (a header) and anything in a query
/// string stay out.
/// </remarks>
public sealed class HttpRequestLog : IDisposable
{
    public const string SourceName = "System.Net.Http";

    /// <summary>How many recent requests are kept, for <see cref="Slowest"/>.</summary>
    internal const int RecentCount = 50;

    private readonly DiagnosticsLog _log;
    private readonly Func<string?> _serverHost;
    private readonly ActivityListener _listener;
    private readonly object _gate = new();
    private readonly Queue<Request> _recent = new();

    /// <param name="serverHost">The LibreNMS server's host, so its requests read "LibreNMS" and others by their host.</param>
    public HttpRequestLog(DiagnosticsLog log, Func<string?> serverHost)
    {
        _log = log;
        _serverHost = serverHost;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = Record,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>One request, as the diagnostics keep it.</summary>
    public sealed record Request(string Method, string Host, string Path, int? Status, string? Error, TimeSpan Duration, DateTimeOffset Ended)
    {
        public bool Failed => Error is not null || Status is null or >= 400;

        /// <summary>"GET /api/v0/devices · 200 · 412 ms".</summary>
        public string Summary => string.Create(CultureInfo.InvariantCulture,
            $"{Method} {Path} · {(Status is { } status ? status.ToString(CultureInfo.InvariantCulture) : Error ?? "no answer")} · {Duration.TotalMilliseconds:0} ms");
    }

    /// <summary>The slowest requests that ended since <paramref name="since"/> - the likely reason a page was slow.</summary>
    public IReadOnlyList<Request> Slowest(DateTimeOffset since, int count = 3)
    {
        lock (_gate)
        {
            return _recent.Where(r => r.Ended >= since).OrderByDescending(r => r.Duration).Take(count).ToList();
        }
    }

    /// <summary>A finished request, from its activity's tags.</summary>
    internal void Record(Activity activity)
    {
        var url = Tag(activity, "url.full");
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var status = int.TryParse(Tag(activity, "http.response.status_code"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : (int?)null;
        var error = Tag(activity, "error.type");
        var request = new Request(
            Tag(activity, "http.request.method") ?? "GET",
            uri.Host,
            uri.AbsolutePath,
            status,
            status is null || status >= 400 ? (error is { Length: > 0 } && !int.TryParse(error, out _) ? error : null) : null,
            activity.Duration,
            activity.StartTimeUtc.Add(activity.Duration));
        Record(request);
    }

    internal void Record(Request request)
    {
        lock (_gate)
        {
            _recent.Enqueue(request);
            while (_recent.Count > RecentCount)
            {
                _recent.Dequeue();
            }
        }

        var who = string.Equals(request.Host, _serverHost(), StringComparison.OrdinalIgnoreCase) ? "LibreNMS" : request.Host;
        if (request.Failed)
        {
            _log.NoteImportant(who + " [warning]", request.Summary);
        }
        else
        {
            _log.Note(who, request.Summary);
        }
    }

    public void Dispose() => _listener.Dispose();

    private static string? Tag(Activity activity, string name) => activity.GetTagItem(name)?.ToString();
}
