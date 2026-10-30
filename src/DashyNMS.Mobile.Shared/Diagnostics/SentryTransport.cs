using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// Posts a report to Sentry as a small envelope of our own (#217), as desktop
/// does (DashyNMS/desktop#62), rather than through Sentry.Maui: the SDK would
/// add device details, breadcrumbs and sessions on its own, and nothing
/// leaves the phone that <see cref="ErrorPayload"/> didn't allow.
/// </summary>
/// <remarks>
/// The DSN is the build's (<c>DASHYNMS_SENTRY_DSN</c>, from the store build
/// environments), never the repository's. A build without one, or with one
/// that isn't a plain https Sentry DSN, is unavailable and sends nothing.
/// </remarks>
public sealed class SentryTransport : IDiagnosticsTransport, IDisposable
{
    private readonly HttpClient _client;
    private readonly Uri? _endpoint;
    private readonly string? _publicKey;

    public SentryTransport(string? dsn)
        : this(dsn, new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false })
    {
    }

    public SentryTransport(string? dsn, HttpMessageHandler handler)
    {
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(10) };
        if (!Uri.TryCreate(dsn, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(uri.UserInfo) || uri.UserInfo.Contains(':')
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return;
        }

        var separator = uri.AbsolutePath.LastIndexOf('/');
        var project = uri.AbsolutePath[(separator + 1)..];
        if (project.Length == 0 || !project.All(char.IsAsciiDigit) || !uri.UserInfo.All(char.IsAsciiLetterOrDigit))
        {
            return;
        }

        _publicKey = uri.UserInfo;
        _endpoint = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Path = uri.AbsolutePath[..(separator + 1)] + "api/" + project + "/envelope/",
        }.Uri;
    }

    public bool IsAvailable => _endpoint is not null;

    public async Task<SendResult> SendAsync(DiagnosticReport report, CancellationToken cancellationToken)
    {
        if (_endpoint is null || report is not ErrorReport error || !ErrorPayload.IsValid(report))
        {
            return new SendResult(SendDisposition.Rejected);
        }

        var payload = Event(error);
        using var body = new MemoryStream();
        WriteJsonLine(body, new { event_id = report.EventId.ToString("N") });
        WriteJsonLine(body, new { type = "event", length = payload.Length });
        body.Write(payload);
        body.WriteByte((byte)'\n');
        if (body.Length > ErrorPayload.MaximumBytes)
        {
            return new SendResult(SendDisposition.Rejected);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.TryAddWithoutValidation("X-Sentry-Auth", "Sentry sentry_version=7,sentry_key=" + _publicKey);
        request.Content = new ByteArrayContent(body.ToArray());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-sentry-envelope");
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new SendResult(SendDisposition.Accepted, RetryDelay(response, "error"));
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new SendResult(SendDisposition.Retry, RetryDelay(response, "error") ?? TimeSpan.FromMinutes(1));
            }

            // A 4xx won't get better by sending again; a 5xx might.
            return new SendResult((int)response.StatusCode >= 500 ? SendDisposition.Retry : SendDisposition.Rejected);
        }
        catch (HttpRequestException)
        {
            return new SendResult(SendDisposition.Retry);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SendResult(SendDisposition.Retry);
        }
    }

    /// <summary>
    /// The event: level, release, the platform as tags, and each exception's
    /// type and frames. Its value is a fixed phrase - the message is never sent.
    /// </summary>
    internal static byte[] Event(ErrorReport report) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        event_id = report.EventId.ToString("N"),
        timestamp = report.Timestamp.ToUnixTimeMilliseconds() / 1000.0,
        platform = "csharp",
        level = report.Origin switch
        {
            ErrorOrigin.Unhandled => "fatal",
            ErrorOrigin.UnobservedTask => "error",
            _ => "warning",
        },
        release = report.Context.Release,
        tags = new Dictionary<string, string>
        {
            ["operating_system"] = report.Context.OperatingSystem,
            ["os_version"] = report.Context.OsVersion,
            ["architecture"] = report.Context.Architecture,
            ["origin"] = report.Origin.ToString(),
        },
        exception = new
        {
            // Sentry lists the outermost last.
            values = report.Exceptions.Reverse().Select(exception => new
            {
                type = exception.Type,
                value = "Managed application error",
                mechanism = new { type = "generic", handled = report.Origin != ErrorOrigin.Unhandled },
                stacktrace = new { frames = exception.Frames.Select(Frame) },
            }),
        },
    });

    public void Dispose() => _client.Dispose();

    private static Dictionary<string, object> Frame(ErrorFrame frame)
    {
        var result = new Dictionary<string, object>
        {
            ["module"] = frame.Module,
            ["function"] = frame.Type + "." + frame.Function,
            ["in_app"] = true,
        };
        if (frame.FileName is { } file)
        {
            result["filename"] = file;
        }

        if (frame.LineNumber is { } line)
        {
            result["lineno"] = line;
        }

        if (frame.IlOffset is { } offset)
        {
            result["data"] = new Dictionary<string, int> { ["il_offset"] = offset };
        }

        return result;
    }

    /// <summary>How long Sentry asks us to wait, from Retry-After or its own rate-limit header, at most a day.</summary>
    private static TimeSpan? RetryDelay(HttpResponseMessage response, string category)
    {
        var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
        if (response.Headers.TryGetValues("X-Sentry-Rate-Limits", out var values))
        {
            foreach (var limit in values.SelectMany(value => value.Split(',')))
            {
                var parts = limit.Split(':');
                if (parts.Length < 2 || (parts[1].Length != 0 && !parts[1].Split(';').Contains(category)))
                {
                    continue;
                }

                if (double.TryParse(parts[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
                    && double.IsFinite(seconds) && seconds > 0)
                {
                    delay = TimeSpan.FromSeconds(Math.Max(delay?.TotalSeconds ?? 0, Math.Min(seconds, 86400)));
                }
            }
        }

        return delay is null ? null : TimeSpan.FromSeconds(Math.Clamp(delay.Value.TotalSeconds, 1, 86400));
    }

    private static void WriteJsonLine(Stream stream, object value)
    {
        stream.Write(JsonSerializer.SerializeToUtf8Bytes(value));
        stream.WriteByte((byte)'\n');
    }
}
