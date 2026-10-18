namespace SpendSense.Common.Security;

/// <summary>MAUI SecureStorage: the Keychain on Apple platforms, Keystore-backed preferences on Android.</summary>
public sealed class SecureStorageSecretStore(ISecureStorage secureStorage) : ISecretStore
{
    public async Task<byte[]?> Get(string name)
    {
        var value = await secureStorage.GetAsync(name);
        return value is null ? null : Convert.FromBase64String(value);
    }

    public Task Set(string name, byte[] value) => secureStorage.SetAsync(name, Convert.ToBase64String(value));

    public Task Remove(string name)
    {
        secureStorage.Remove(name);
        return Task.CompletedTask;
    }
}
