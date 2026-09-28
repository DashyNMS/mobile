namespace DashyNMS.Mobile.Security;

/// <summary>
/// The platform's own secret store - Keychain on iOS, the Android Keystore
/// on Android. The app head adapts MAUI's SecureStorage to this.
/// </summary>
public interface ISecureStorage
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    /// <summary>Returns true when there was something to remove.</summary>
    bool Remove(string key);
}
