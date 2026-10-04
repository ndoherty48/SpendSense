using Microsoft.Maui.Storage;

namespace SpendSense.Tests.TestDoubles;

/// <summary>A dictionary-backed <see cref="IPreferences"/>: MAUI's real one needs a device.</summary>
public sealed class InMemoryPreferences : IPreferences
{
    readonly Dictionary<string, object?> values = [];

    static string Key(string key, string? sharedName) => sharedName is null ? key : $"{sharedName}:{key}";

    public bool ContainsKey(string key, string? sharedName = null) => values.ContainsKey(Key(key, sharedName));

    public void Remove(string key, string? sharedName = null) => values.Remove(Key(key, sharedName));

    public void Clear(string? sharedName = null) => values.Clear();

    public void Set<T>(string key, T value, string? sharedName = null) => values[Key(key, sharedName)] = value;

    public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
        values.TryGetValue(Key(key, sharedName), out var value) && value is T typed ? typed : defaultValue;
}
