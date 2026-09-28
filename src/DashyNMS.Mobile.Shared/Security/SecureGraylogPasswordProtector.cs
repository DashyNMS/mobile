using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Security;

/// <summary>
/// Mobile's <see cref="IGraylogPasswordProtector"/>: the Graylog password (or
/// the word "token" beside an access token) lives in the platform
/// keychain/keystore, under its own key, rather than desktop's DPAPI file.
/// </summary>
public sealed class SecureGraylogPasswordProtector : IGraylogPasswordProtector
{
    public const string Key = "graylog.password";

    private readonly SecretCache _secrets;

    public SecureGraylogPasswordProtector(SecretCache secrets) => _secrets = secrets;

    public bool HasStoredPassword => _secrets.Contains(Key);

    public string? Load() => _secrets.Get(Key);

    public void Save(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        _secrets.Set(Key, password);
    }

    public void Clear() => _secrets.Remove(Key);
}
