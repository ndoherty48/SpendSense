using System.Globalization;
using System.Security.Cryptography;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Interceptors;

namespace SpendSense.Common.Security;

public enum DatabaseState
{
    Ready,

    /// <summary>The database is encrypted and the device key is gone or wrong: show the recovery screen.</summary>
    KeyMissing
}

/// <summary>
/// Opens the encrypted database before any page renders (ADR 0004 decisions 2–4): loads or creates the device key,
/// encrypts data left from before encryption, checks the key, and migrates.
/// </summary>
public class DatabaseOpener(ISecretStore secrets, IFileSystem fileSystem, DatabaseKey key)
{
    public const string DeviceKeyName = "spendsense.db.device-key";

    public async Task<DatabaseState> Open()
    {
        var path = DatabasePaths.Database(fileSystem);
        Directory.CreateDirectory(DatabasePaths.Folder(fileSystem));

        // A zero-length file is what SQLite leaves when it was opened but never written: treat it as no database.
        var exists = File.Exists(path) && new FileInfo(path).Length > 0;
        var plaintext = exists && SqliteCipher.IsPlaintext(path);

        var stored = await secrets.Get(DeviceKeyName);
        if (stored is null)
        {
            if (exists && !plaintext)
                return DatabaseState.KeyMissing;

            // Stored before any file is encrypted with it: a crash part-way leaves a key and a plaintext file,
            // which the next launch encrypts again. The other order could leave an encrypted file with no key.
            stored = RandomNumberGenerator.GetBytes(DatabaseKey.Size);
            await secrets.Set(DeviceKeyName, stored);
        }
        key.Load(stored);

        if (plaintext)
            await Task.Run(() => EncryptExistingData(path));

        if (exists && !SqliteCipher.Opens(path, key.Value))
        {
            key.Unload();
            return DatabaseState.KeyMissing;
        }

        await using var db = new SpendSenseDbContext(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite($"Data Source={path}")
            .AddInterceptors(new DatabaseKeyInterceptor(key))
            .Options);
        await db.Database.MigrateAsync();
        return DatabaseState.Ready;
    }

    /// <summary>
    /// Moves the unreadable database aside (kept, never deleted, in case the key comes back) and starts again with a
    /// new key and an empty database. Returns the folder the old data went to.
    /// </summary>
    public async Task<string> StartFresh()
    {
        SqliteConnection.ClearAllPools();
        var locked = Path.Join(DatabasePaths.Locked(fileSystem), DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(locked);

        foreach (var file in DatabasePaths.WithSidecars(DatabasePaths.Database(fileSystem)).Where(File.Exists))
            File.Move(file, Path.Join(locked, Path.GetFileName(file)));

        // Safety copies were made with the old key, so they belong with the old data.
        var safetyCopies = DatabasePaths.SafetyCopies(fileSystem);
        if (Directory.Exists(safetyCopies))
            Directory.Move(safetyCopies, Path.Join(locked, "Backups"));

        await secrets.Remove(DeviceKeyName);
        key.Unload();

        if (await Open() != DatabaseState.Ready)
            throw new InvalidOperationException("A new database couldn't be created.");
        return locked;
    }

    /// <summary>
    /// First launch after encryption arrived (ADR 0004 decision 3): the database and its safety copies are encrypted
    /// in place, and old plaintext exports are deleted.
    /// </summary>
    void EncryptExistingData(string path)
    {
        SqliteConnection.ClearAllPools();
        SqliteCipher.EncryptInPlace(path, key.Value, wal: true);

        var safetyCopies = DatabasePaths.SafetyCopies(fileSystem);
        if (Directory.Exists(safetyCopies))
        {
            foreach (var copy in Directory.GetFiles(safetyCopies, "*.db").Where(SqliteCipher.IsPlaintext))
                SqliteCipher.EncryptInPlace(copy, key.Value, wal: false);
        }

        foreach (var cache in new[] { "exports", "imports" }.Select(name => Path.Join(fileSystem.CacheDirectory, name)))
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }
}
