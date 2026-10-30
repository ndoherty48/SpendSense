namespace SpendSense.Common.Security;

/// <summary>
/// Platform secure storage for keys (Keychain, Android Keystore, DPAPI on Windows). Injected so the database and
/// backup code runs in tests. Never use <see cref="IPreferences"/> for anything in here.
/// </summary>
public interface ISecretStore
{
    Task<byte[]?> Get(string name);

    Task Set(string name, byte[] value);

    Task Remove(string name);
}
