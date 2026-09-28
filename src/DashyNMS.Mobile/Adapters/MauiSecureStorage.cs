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
        var value = await SecureStorage.Default.GetAsync(key);

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

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public bool Remove(string key) => SecureStorage.Default.Remove(key);
}
