namespace DashyNMS.Mobile.Adapters;

/// <summary><see cref="Security.ISecureStorage"/> over MAUI's SecureStorage: Keychain on iOS, Keystore-backed on Android.</summary>
/// <remarks>
/// On iOS, secrets are kept "this device only" (#6): MAUI's default lets
/// keychain entries travel in encrypted and iCloud backups and be restored
/// onto another phone. "After first unlock" stays, so background alert
/// checks can still read the token while the phone is locked. Android's are
/// already device-bound (Keystore keys, and allowBackup is off).
/// </remarks>
public sealed class MauiSecureStorage : Security.ISecureStorage
{
#if IOS
    /// <summary>Set once a secret saved before this change has been re-saved with the new setting.</summary>
    private const string MovedPrefix = "keychain-this-device-only:";
#endif

    public MauiSecureStorage()
    {
#if IOS
        SecureStorage.DefaultAccessible = global::Security.SecAccessible.AfterFirstUnlockThisDeviceOnly;
#endif
    }

    public async Task<string?> GetAsync(string key)
    {
#if IOS
        var value = ReadKeychain(key);
#else
        var value = await SecureStorage.Default.GetAsync(key);
#endif

#if IOS
        // An entry saved by an earlier version keeps its old accessibility
        // until it's written again; MAUI's set replaces the entry, so one
        // re-save moves it over.
        if (!string.IsNullOrEmpty(value) && !Preferences.Default.Get(MovedPrefix + key, false))
        {
            await SecureStorage.Default.SetAsync(key, value);
            Preferences.Default.Set(MovedPrefix + key, true);
        }
#endif

        return value;
    }

#if IOS
    /// <summary>
    /// MAUI 10.0.120 maps every failed query to null. A chat key may only be
    /// created after confirmed absence, so preserve native read failures here.
    /// The service matches MAUI's pinned preferences alias, retaining existing
    /// entries and the this-device-only migration above.
    /// </summary>
    private static string? ReadKeychain(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var query = new global::Security.SecRecord(global::Security.SecKind.GenericPassword)
        {
            Account = key,
            Service = $"{AppInfo.Current.PackageName}.microsoft.maui.essentials.preferences",
        };
        using var match = global::Security.SecKeyChain.QueryAsRecord(query, out var status);
        if (status == global::Security.SecStatusCode.ItemNotFound) return null;
        if (status != global::Security.SecStatusCode.Success || match?.ValueData is null)
            throw new InvalidOperationException("Secure storage could not be read.");
        return Foundation.NSString.FromData(match.ValueData, Foundation.NSStringEncoding.UTF8)
            ?? throw new InvalidOperationException("Secure storage contained invalid text.");
    }
#endif

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public bool Remove(string key) => SecureStorage.Default.Remove(key);
}
