using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Security;

/// <summary>
/// Mobile's <see cref="IUnimusTokenProtector"/>: the Unimus API token lives in
/// the platform keychain/keystore under its own key, rather than desktop's
/// DPAPI file, as the Graylog password does.
/// </summary>
public sealed class SecureUnimusTokenProtector : IUnimusTokenProtector
{
    public const string Key = "unimus.token";

    private readonly SecretCache _secrets;

    public SecureUnimusTokenProtector(SecretCache secrets) => _secrets = secrets;

    public bool HasStoredToken => _secrets.Contains(Key);

    public string? Load() => _secrets.Get(Key);

    public void Save(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        _secrets.Set(Key, token);
    }

    public void Clear() => _secrets.Remove(Key);
}
