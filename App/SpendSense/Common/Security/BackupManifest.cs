using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpendSense.Common.Security;

/// <summary>
/// <c>manifest.json</c> in a backup zip (ADR 0004 decision 5). Plaintext, with no personal data: just enough to
/// recognise the file and unwrap its key.
/// </summary>
public sealed record BackupManifest(string Format, int FormatVersion, DateTime CreatedUtc, string Cipher, IReadOnlyList<KeySlot> KeySlots)
{
    public const string FormatName = "spendsense-backup";

    public const int CurrentFormatVersion = 1;

    public const string CipherName = "sqlite3mc-chacha20";

    public const string EntryName = "manifest.json";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// What each key slot is bound to: changing the format, version, date, cipher or slot type makes unwrapping fail.
    /// </summary>
    public byte[] AssociatedData(string slotType) =>
        Encoding.UTF8.GetBytes(string.Join('|', Format, FormatVersion.ToString(CultureInfo.InvariantCulture),
            CreatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), Cipher, slotType));
}

/// <summary>The backup key, wrapped with AES-256-GCM under a key derived from a password or a recovery code.</summary>
public sealed record KeySlot(string Type, string Kdf, int? Iterations, byte[] Salt, byte[] Nonce, byte[] WrappedKey)
{
    public const string Password = "password";

    public const string RecoveryCode = "recoveryCode";
}
