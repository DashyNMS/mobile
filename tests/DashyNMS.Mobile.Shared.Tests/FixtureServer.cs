using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DashyNMS.Mobile.Tests;

internal sealed record Answer(int Status, string Body)
{
    /// <summary>Accept the request and never answer it.</summary>
    public static readonly Answer Hang = new(0, "");
}

/// <summary>
/// A loopback LibreNMS answering from the fixture, recording each request's path under
/// <c>/api/v0/</c>. Requests without the connection's token are refused, as LibreNMS would.
/// </summary>
internal sealed class FixtureServer : IDisposable
{
    /// <summary>The token every fixture connection uses; any other is refused.</summary>
    internal const string Token = "test-token";

    private readonly HttpListener listener = new();
    private readonly ConcurrentQueue<string> requests = new();
    private readonly Func<string, Answer?> answer;
    private readonly CancellationTokenSource stopping = new();

    public FixtureServer(Func<string, Answer?> answer)
    {
        this.answer = answer;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            Root = new Uri($"http://127.0.0.1:{((IPEndPoint)probe.LocalEndpoint).Port}/");
            probe.Stop();
        }
        listener.Prefixes.Add(Root.ToString());
        listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public Uri Root { get; }

    /// <summary>A loopback address that was free a moment ago, so a connection is refused.</summary>
    internal static Uri NothingListening()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return new Uri($"http://127.0.0.1:{port}/");
    }

    public string[] Requests => requests.ToArray();

    private async Task ServeAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (Exception) { return; }
            _ = Task.Run(() => RespondAsync(context));
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var path = context.Request.Url!.AbsolutePath;
        const string api = "/api/v0/";
        var relative = path.StartsWith(api, StringComparison.Ordinal) ? path[api.Length..] : path;
        requests.Enqueue(relative);
        var reply = context.Request.Headers["X-Auth-Token"] == Token
            ? answer(relative) ?? new Answer(404, """{"status":"error","message":"No fixture for this path"}""")
            : new Answer(401, """{"status":"error","message":"Unauthenticated."}""");
        try
        {
            if (reply == Answer.Hang)
            {
                await Task.Delay(System.Threading.Timeout.Infinite, stopping.Token);
                return;
            }
            var body = Encoding.UTF8.GetBytes(reply.Body);
            context.Response.StatusCode = reply.Status;
            context.Response.ContentType = reply.Body.StartsWith('<') ? "text/html" : "application/json";
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }
        // The client gave up (a timeout) or the server is stopping.
        catch (Exception) { }
    }

    public void Dispose()
    {
        stopping.Cancel();
        listener.Stop();
        listener.Close();
    }
}
