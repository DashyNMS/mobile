
namespace DashyNMS.Mobile.Adapters;

/// <summary><see cref="Security.ISecureStorage"/> over MAUI's SecureStorage: Keychain on iOS, Keystore-backed on Android.</summary>
public sealed class MauiSecureStorage : Security.ISecureStorage
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public bool Remove(string key) => SecureStorage.Default.Remove(key);
}
