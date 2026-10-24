using System.Security.Cryptography;

using SpendSense.Common.Security;

namespace SpendSense.Platforms.Windows;

/// <summary>
/// Secret store for the unpackaged Windows app, where MAUI SecureStorage needs package identity (ADR 0004).
/// Values are encrypted with DPAPI (current-user scope) and written under the app data folder, so only the same
/// Windows user on the same machine can read them.
/// </summary>
public sealed class DpapiSecretStore(IFileSystem fileSystem) : ISecretStore
{
    static readonly byte[] Entropy = "dev.nathandoherty.spendsense"u8.ToArray();

    string Folder => Path.Combine(fileSystem.AppDataDirectory, "SpendSense", "secrets");

    public async Task<byte[]?> Get(string name)
    {
        var file = PathFor(name);
        if (!File.Exists(file))
            return null;
        return ProtectedData.Unprotect(await File.ReadAllBytesAsync(file), Entropy, DataProtectionScope.CurrentUser);
    }

    public async Task Set(string name, byte[] value)
    {
        Directory.CreateDirectory(Folder);
        // Write then rename, so a crash never leaves a half-written key.
        var temp = PathFor(name) + ".tmp";
        await File.WriteAllBytesAsync(temp, ProtectedData.Protect(value, Entropy, DataProtectionScope.CurrentUser));
        File.Move(temp, PathFor(name), overwrite: true);
    }

    public Task Remove(string name)
    {
        File.Delete(PathFor(name));
        return Task.CompletedTask;
    }

    string PathFor(string name) => Path.Combine(Folder, $"{name}.bin");
}
