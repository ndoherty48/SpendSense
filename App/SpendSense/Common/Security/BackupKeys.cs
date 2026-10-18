using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpendSense.Common.Security;

public enum UnlockMethod
{
    Password,
    RecoveryCode
}

/// <summary>
/// The backup password and recovery code (ADR 0004 decision 5). Only the keys derived from them, and their salts,
/// are kept, in <see cref="ISecretStore"/>; the password and the code themselves are never stored.
/// </summary>
public class BackupKeys(ISecretStore secrets)
{
    public const string StoreName = "spendsense.backup.keys";

    public const int MinPasswordLength = 10;

    const int KeySize = 32;
    const int SaltSize = 16;
    const int NonceSize = 12;
    const int TagSize = 16;
    static readonly byte[] RecoveryInfo = "spendsense-backup-recovery-code"u8.ToArray();

    /// <summary>PBKDF2-SHA256 iterations for new passwords (OWASP 2023). Tests lower it; the manifest records it.</summary>
    public int PasswordIterations { get; init; } = 600_000;

    sealed record Remembered(int Iterations, byte[] PasswordSalt, byte[] PasswordKey, byte[] RecoverySalt, byte[] RecoveryKey);

    public async Task<bool> IsSetUp() => await Load() is not null;

    /// <summary>
    /// Sets (or replaces) the backup password and makes a new recovery code, returned formatted for showing once.
    /// Backups made before keep the password and code they were made with.
    /// </summary>
    public async Task<string> SetPassword(string password)
    {
        if (password.Length < MinPasswordLength)
            throw new ArgumentException($"Use at least {MinPasswordLength} characters.", nameof(password));

        var passwordSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var iterations = PasswordIterations;
        var passwordKey = await Task.Run(() => DerivePasswordKey(password, passwordSalt, iterations));

        var code = RandomNumberGenerator.GetBytes(RecoveryCode.Size);
        var recoverySalt = RandomNumberGenerator.GetBytes(SaltSize);

        await secrets.Set(StoreName, JsonSerializer.SerializeToUtf8Bytes(
            new Remembered(iterations, passwordSalt, passwordKey, recoverySalt, DeriveRecoveryKey(code, recoverySalt))));
        return RecoveryCode.Format(code);
    }

    /// <summary>A manifest for a new backup whose database is encrypted with <paramref name="backupKey"/>.</summary>
    public async Task<BackupManifest> CreateManifest(byte[] backupKey, DateTime createdUtc)
    {
        var remembered = await Load() ?? throw new InvalidOperationException("Set a backup password first.");
        var manifest = new BackupManifest(BackupManifest.FormatName, BackupManifest.CurrentFormatVersion,
            // Whole seconds, so the date reads back from JSON exactly and the associated data matches.
            new DateTime(createdUtc.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc),
            BackupManifest.CipherName, []);

        return manifest with
        {
            KeySlots =
            [
                Wrap(manifest, KeySlot.Password, "PBKDF2-SHA256", remembered.Iterations, remembered.PasswordSalt, remembered.PasswordKey, backupKey),
                Wrap(manifest, KeySlot.RecoveryCode, "HKDF-SHA256", null, remembered.RecoverySalt, remembered.RecoveryKey, backupKey)
            ]
        };
    }

    /// <summary>The backup key, using the keys remembered on this device, or null if they don't open this backup.</summary>
    public async Task<byte[]?> UnlockWithRemembered(BackupManifest manifest)
    {
        if (await Load() is not { } remembered)
            return null;

        foreach (var slot in manifest.KeySlots)
        {
            var key = slot.Type switch
            {
                KeySlot.Password when slot.Iterations == remembered.Iterations && slot.Salt.AsSpan().SequenceEqual(remembered.PasswordSalt) => remembered.PasswordKey,
                KeySlot.RecoveryCode when slot.Salt.AsSpan().SequenceEqual(remembered.RecoverySalt) => remembered.RecoveryKey,
                _ => null
            };
            if (key is not null && Unwrap(manifest, slot, key) is { } backupKey)
                return backupKey;
        }
        return null;
    }

    /// <summary>The backup key from a typed password or recovery code, or null if it doesn't match.</summary>
    public async Task<byte[]?> Unlock(BackupManifest manifest, UnlockMethod method, string secret)
    {
        var type = method == UnlockMethod.Password ? KeySlot.Password : KeySlot.RecoveryCode;
        if (manifest.KeySlots.FirstOrDefault(s => s.Type == type) is not { } slot)
            return null;

        byte[] key;
        if (method == UnlockMethod.Password)
        {
            // A crafted manifest can only make its own slot weaker; the cap stops it hanging the app instead.
            if (slot.Iterations is not (>= 1 and <= 10_000_000))
                return null;
            key = await Task.Run(() => DerivePasswordKey(secret, slot.Salt, slot.Iterations.Value));
        }
        else
        {
            if (!RecoveryCode.TryParse(secret, out var code))
                return null;
            key = DeriveRecoveryKey(code, slot.Salt);
        }
        return Unwrap(manifest, slot, key);
    }

    static byte[] DerivePasswordKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)), salt, iterations, HashAlgorithmName.SHA256, KeySize);

    static byte[] DeriveRecoveryKey(byte[] code, byte[] salt) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, code, KeySize, salt, RecoveryInfo);

    static KeySlot Wrap(BackupManifest manifest, string type, string kdf, int? iterations, byte[] salt, byte[] wrappingKey, byte[] backupKey)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var sealedKey = new byte[backupKey.Length + TagSize];
        using var aes = new AesGcm(wrappingKey, TagSize);
        aes.Encrypt(nonce, backupKey, sealedKey.AsSpan(0, backupKey.Length), sealedKey.AsSpan(backupKey.Length), manifest.AssociatedData(type));
        return new KeySlot(type, kdf, iterations, salt, nonce, sealedKey);
    }

    static byte[]? Unwrap(BackupManifest manifest, KeySlot slot, byte[] wrappingKey)
    {
        if (slot.Nonce is not { Length: NonceSize } || slot.WrappedKey is not { Length: KeySize + TagSize })
            return null;
        var backupKey = new byte[KeySize];
        try
        {
            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Decrypt(slot.Nonce, slot.WrappedKey.AsSpan(0, KeySize), slot.WrappedKey.AsSpan(KeySize), backupKey, manifest.AssociatedData(slot.Type));
            return backupKey;
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }
    }

    async Task<Remembered?> Load() =>
        await secrets.Get(StoreName) is { } bytes ? JsonSerializer.Deserialize<Remembered>(bytes) : null;
}
