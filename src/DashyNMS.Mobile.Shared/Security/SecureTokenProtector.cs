using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Security;

/// <summary>
/// Mobile's <see cref="ITokenProtector"/>: the LibreNMS API token lives in the
/// platform keychain/keystore rather than a DPAPI file.
/// </summary>
public sealed class SecureTokenProtector : ITokenProtector
{
    public const string Key = "librenms.api-token";

    private readonly SecretCache _secrets;

    public SecureTokenProtector(SecretCache secrets) => _secrets = secrets;

    public bool HasStoredToken => _secrets.Contains(Key);

    public string? Load() => _secrets.Get(Key);

    public void Save(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _secrets.Set(Key, token);
    }

    public void Clear() => _secrets.Remove(Key);
}
