using System.Globalization;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Services;

/// <summary>One item a bulk action couldn't do, and why.</summary>
public sealed record BulkFailure(string Name, string Reason);

/// <summary>How a bulk action went: how many it tried, which worked, and which didn't.</summary>
public sealed record BulkResult<T>(IReadOnlyList<T> Succeeded, IReadOnlyList<BulkFailure> Failures)
{
    public int Attempted => Succeeded.Count + Failures.Count;

    public bool AllSucceeded => Failures.Count == 0;

    /// <summary>
    /// "Acknowledged 14 alerts." - or, when some failed, "Acknowledged 12 of
    /// 14 alerts. 2 failed: core-sw-01 (HTTP 500), edge-rtr (timed out)." (#85)
    /// </summary>
    /// <param name="done">What was done, in the past tense: "Acknowledged".</param>
    /// <param name="noun">One item: "alert" (an "s" is added for more).</param>
    public string Describe(string done, string noun)
    {
        var nouns = Attempted == 1 ? noun : noun + "s";
        if (AllSucceeded)
        {
            return string.Create(CultureInfo.CurrentCulture, $"{done} {Attempted} {nouns}.");
        }

        var reasons = string.Join(", ", Failures.Take(3).Select(f => $"{f.Name} ({f.Reason})"));
        var more = Failures.Count > 3 ? string.Create(CultureInfo.CurrentCulture, $" and {Failures.Count - 3} more") : string.Empty;
        return string.Create(
            CultureInfo.CurrentCulture,
            $"{done} {Succeeded.Count} of {Attempted} {nouns}. {Failures.Count} failed: {reasons}{more}.");
    }
}

/// <summary>
/// Runs one action over several items (#85) - acknowledging alerts,
/// rediscovering devices - one at a time, so LibreNMS isn't asked for them
/// all at once, reporting progress, and carrying on past a failure so one
/// bad item doesn't stop the rest.
/// </summary>
public static class BulkRun
{
    /// <param name="name">How an item is named in the summary of failures.</param>
    /// <param name="progress">Told how many are done, after each - straight away, on the caller's thread.</param>
    public static async Task<BulkResult<T>> RunAsync<T>(
        IReadOnlyList<T> items,
        Func<T, string> name,
        Func<T, Task> action,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var succeeded = new List<T>();
        var failures = new List<BulkFailure>();
        var done = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await action(item).ConfigureAwait(true);
                succeeded.Add(item);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new BulkFailure(name(item), Reason(ex)));
            }

            progress?.Invoke(++done);
        }

        return new BulkResult<T>(succeeded, failures);
    }

    /// <summary>Short enough to list several: the status code, or the first sentence of the reason.</summary>
    private static string Reason(Exception ex)
    {
        if (ex is DesktopNMS.Core.Api.LibreNmsApiException { StatusCode: { } status })
        {
            return string.Create(CultureInfo.InvariantCulture, $"HTTP {(int)status}");
        }

        var text = ViewModelBase.Describe(ex);
        var stop = text.IndexOf(". ", StringComparison.Ordinal);
        return (stop > 0 ? text[..stop] : text).TrimEnd('.');
    }
}
