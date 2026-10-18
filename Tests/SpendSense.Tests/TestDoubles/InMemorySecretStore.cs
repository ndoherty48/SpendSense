using SpendSense.Common.Security;

namespace SpendSense.Tests.TestDoubles;

/// <summary>A dictionary-backed <see cref="ISecretStore"/>: the real ones need a Keychain, Keystore or DPAPI.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    readonly Dictionary<string, byte[]> values = [];

    public Task<byte[]?> Get(string name) => Task.FromResult(values.TryGetValue(name, out var value) ? value.ToArray() : null);

    public Task Set(string name, byte[] value)
    {
        values[name] = value.ToArray();
        return Task.CompletedTask;
    }

    public Task Remove(string name)
    {
        values.Remove(name);
        return Task.CompletedTask;
    }
}
