using Microsoft.Maui.Storage;

namespace SpendSense.Tests.TestDoubles;

/// <summary>An <see cref="IFileSystem"/> rooted in a fresh temporary folder, deleted on dispose.</summary>
public sealed class TempFileSystem : IFileSystem, IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "spendsense-tests", Guid.NewGuid().ToString("N"));

    public TempFileSystem()
    {
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(AppDataDirectory);
    }

    public string CacheDirectory => Path.Combine(root, "cache");

    public string AppDataDirectory => Path.Combine(root, "data");

    public Task<Stream> OpenAppPackageFileAsync(string filename) => throw new NotSupportedException();

    public Task<bool> AppPackageFileExistsAsync(string filename) => Task.FromResult(false);

    /// <summary>A path for a scratch file inside the temporary folder.</summary>
    public string PathFor(string name) => Path.Combine(root, name);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }
}
