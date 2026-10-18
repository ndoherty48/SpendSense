namespace SpendSense.Common.Security;

/// <summary>
/// Debug builds on Mac Catalyst only (ADR 0004): the Keychain refuses unsigned builds without the
/// <c>keychain-access-groups</c> entitlement, so when it fails, secrets go to plain files in
/// <paramref name="directory"/> instead. Never registered in a Release build.
/// </summary>
public sealed class DevFallbackSecretStore(ISecretStore primary, string directory) : ISecretStore
{
    bool usingFiles;

    public async Task<byte[]?> Get(string name)
    {
        if (!usingFiles)
        {
            try
            {
                var value = await primary.Get(name);
                if (value is not null || !File.Exists(PathFor(name)))
                    return value;
            }
            catch (Exception ex)
            {
                Fallback(ex);
            }
        }
        return File.Exists(PathFor(name)) ? await File.ReadAllBytesAsync(PathFor(name)) : null;
    }

    public async Task Set(string name, byte[] value)
    {
        if (!usingFiles)
        {
            try
            {
                await primary.Set(name, value);
                return;
            }
            catch (Exception ex)
            {
                Fallback(ex);
            }
        }
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(PathFor(name), value);
    }

    public async Task Remove(string name)
    {
        if (!usingFiles)
        {
            try { await primary.Remove(name); }
            catch (Exception ex) { Fallback(ex); }
        }
        File.Delete(PathFor(name));
    }

    void Fallback(Exception ex)
    {
        usingFiles = true;
        Console.WriteLine($"[Secrets] Secure storage unavailable ({ex.Message}); using the Debug-only file store in {directory}.");
    }

    string PathFor(string name) => Path.Combine(directory, $"{name}.bin");
}
