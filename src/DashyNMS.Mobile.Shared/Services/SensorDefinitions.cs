using DesktopNMS.Core.Sensors;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// The sensor-definitions catalogue (DashyNMS/sensor-definitions) that names
/// state sensors, kept by Core's <see cref="SensorDefinitionStore"/> in the
/// app's data folder rather than a cache the OS may clear (DashyNMS/desktop#468).
/// </summary>
/// <remarks>
/// <para>On when nothing has been chosen, as desktop since DashyNMS/desktop#531.
/// Core checks GitHub at most daily and keeps working from the cached copy
/// offline; the phone only asks when it opens or comes back to the foreground,
/// never from a background alert check.</para>
/// <para>Sign-out keeps the definitions - they hold no account data. "Sign out
/// and forget everything" deletes them with the rest of the data folder, and
/// turning the feature off deletes them at once.</para>
/// </remarks>
public sealed class SensorDefinitions : IDisposable
{
    public SensorDefinitions(string dataDirectory, HttpClient? http = null)
    {
        Store = new SensorDefinitionStore(
            new SensorDefinitionPaths(Path.Combine(dataDirectory, "installation-settings.json"), Path.Combine(dataDirectory, "sensor-definitions")),
            http);
    }

    public SensorDefinitionStore Store { get; }

    /// <summary>Start-up: the saved choice (or official definitions, with none saved), then a check for a newer catalogue.</summary>
    public async Task StartAsync()
    {
        await Store.InitialiseAsync().ConfigureAwait(false);

        // A saved file - even one turning them off, or a damaged one - is the user's choice.
        if (!File.Exists(Store.Paths.SettingsFile))
        {
            try
            {
                await Store.ConfigureAsync(new SensorDefinitionOptions { Enabled = true }).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Settings shows Core's status; the app still starts.
            }
        }

        await CheckAsync(indexOnly: true).ConfigureAwait(false);
    }

    /// <summary>Back in the foreground: Core fetches only if its last check is a day old, and nothing while it's off.</summary>
    public async Task CheckAsync(bool indexOnly = false)
    {
        try
        {
            await Store.SynchroniseAsync(indexOnly ? Array.Empty<string>() : null).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            // Core records the failure in its status and keeps the cached definitions.
        }
    }

    /// <summary>
    /// After "Sign out and forget everything": nothing more fetched this
    /// session, and no choice left behind, so the next launch starts afresh.
    /// </summary>
    public async Task ForgetAsync()
    {
        try
        {
            await Store.ConfigureAsync(new SensorDefinitionOptions { Enabled = false }).ConfigureAwait(false);
            File.Delete(Store.Paths.SettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The data folder has already been wiped; this only stops further requests.
        }
    }

    public void Dispose() => Store.Dispose();
}
