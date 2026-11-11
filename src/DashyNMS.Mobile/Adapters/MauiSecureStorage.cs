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
        // Other secrets use SecretCache's established error-recovery contract.
        // Only the chat key requires confirmed absence before replacement.
        var value = key == Intelligence.IntelligenceChatStorage.KeyName
            ? ReadKeychain(key)
            : await SecureStorage.Default.GetAsync(key);
#elif ANDROID
        var value = key == Intelligence.IntelligenceChatStorage.KeyName
            ? await Task.Run(() => ReadAndroidChatKey(key))
            : await SecureStorage.Default.GetAsync(key);
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

    public Task SetAsync(string key, string value)
    {
#if ANDROID
        if (key == Intelligence.IntelligenceChatStorage.KeyName)
            return Task.Run(() => WriteAndroidChatKey(key, value));
#endif
        return SecureStorage.Default.SetAsync(key, value);
    }

    public bool Remove(string key)
    {
#if ANDROID
        if (key == Intelligence.IntelligenceChatStorage.KeyName)
        {
            using var prefs = AndroidChatPreferences();
            using var editor = prefs.Edit() ?? throw new InvalidOperationException("Secure storage could not be opened.");
            editor.Remove(key);
            // Always change the snapshot, including a retry after a failed
            // commit whose deletion may already be visible in memory.
            editor.PutString("intelligence-chats-commit-v1", Guid.NewGuid().ToString("N"));
            return editor.Commit();
        }
#endif
        return SecureStorage.Default.Remove(key);
    }

#if ANDROID
    /// <summary>
    /// Uses MAUI 10.0.120's existing encrypted store and Keystore alias, without
    /// its destructive recovery on read failure. Only the chat key uses this
    /// checked-commit path; other credentials retain their existing behaviour.
    /// </summary>
    private static global::Android.Content.ISharedPreferences AndroidChatPreferences()
    {
        var context = global::Android.App.Application.Context;
        var alias = $"{AppInfo.Current.PackageName}.microsoft.maui.essentials.preferences";
#pragma warning disable CS0618 // Match the encrypted preferences already used by pinned MAUI.
        using var master = new AndroidX.Security.Crypto.MasterKey.Builder(context, alias)
            .SetKeyScheme(AndroidX.Security.Crypto.MasterKey.KeyScheme.Aes256Gcm)!
            .Build();
        return AndroidX.Security.Crypto.EncryptedSharedPreferences.Create(context, alias, master!,
            AndroidX.Security.Crypto.EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv!,
            AndroidX.Security.Crypto.EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm!)
            ?? throw new InvalidOperationException("Secure storage could not be opened.");
#pragma warning restore CS0618
    }

    private static string? ReadAndroidChatKey(string key)
    {
        using var prefs = AndroidChatPreferences();
        var value = prefs.GetString(key, null);
        if (value is not null)
        {
            // An earlier failed commit can still be visible in memory. A new
            // encrypted write and checked commit must succeed before accepting
            // that value as the key for any saved file.
            CommitAndroidChatKey(prefs, key, value);
        }
        return value;
    }

    private static void WriteAndroidChatKey(string key, string value)
    {
        using var prefs = AndroidChatPreferences();
        CommitAndroidChatKey(prefs, key, value);
    }

    private static void CommitAndroidChatKey(global::Android.Content.ISharedPreferences prefs, string key, string value)
    {
        using var editor = prefs.Edit() ?? throw new InvalidOperationException("Secure storage could not be opened.");
        editor.PutString(key, value);
        if (!editor.Commit()) throw new InvalidOperationException("The chat key could not be saved.");
    }
#endif
}
