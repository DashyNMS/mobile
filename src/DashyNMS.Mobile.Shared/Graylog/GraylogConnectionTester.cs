using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Graylog;

/// <summary>Settings' "Test connection" - so tests can answer without a Graylog server.</summary>
public interface IGraylogConnectionTester
{
    /// <summary>How many streams the account can see, or a <see cref="GraylogApiException"/> saying why not.</summary>
    Task<int> TestAsync(GraylogConnection connection, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tries the form's settings on a throwaway client, as desktop does, so the
/// live <see cref="IGraylogApi"/> isn't touched until Save.
/// </summary>
public sealed class GraylogConnectionTester : IGraylogConnectionTester
{
    public async Task<int> TestAsync(GraylogConnection connection, CancellationToken cancellationToken = default)
    {
        using var probe = new GraylogApi(NullLogger<GraylogApi>.Instance);
        probe.Configure(connection);
        return await probe.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
}
