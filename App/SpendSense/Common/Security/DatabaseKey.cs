namespace SpendSense.Common.Security;

/// <summary>
/// The device key for the life of the process (ADR 0004). <see cref="DatabaseOpener"/> loads it; the
/// <see cref="Data.Interceptors.DatabaseKeyInterceptor"/> applies it. Never logged or put in a connection string.
/// </summary>
public sealed class DatabaseKey
{
    public const int Size = 32;

    byte[]? key;

    public bool IsLoaded => key is not null;

    public byte[] Value => key ?? throw new InvalidOperationException("The database key isn't loaded: DatabaseOpener.Open() runs first.");

    public void Load(byte[] value)
    {
        if (value is not { Length: Size })
            throw new ArgumentException($"The database key must be {Size} bytes.", nameof(value));
        key = value.ToArray();
    }

    public void Unload() => key = null;
}
