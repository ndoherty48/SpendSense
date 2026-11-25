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
    KeyMissing,

    /// <summary>
    /// A newer version of the app has migrated this database past what this build knows: it's left untouched, and the
    /// app asks to be updated rather than running against a schema it doesn't match.
    /// </summary>
    NewerVersion
}

/// <summary>
/// Opens the encrypted database before any page renders (ADR 0005 decisions 2–4): loads or creates the device key,
/// encrypts data left from before encryption, checks the key, refuses data from a newer version, and migrates.
/// </summary>
public class DatabaseOpener(ISecretStore secrets, IFileSystem fileSystem, DatabaseKey key)
{
    public const string DeviceKeyName = "spendsense.db.device-key";

    /// <summary>
    /// Where <see cref="StartFresh"/> keeps the old key, named after the folder the old data went to. A key reported
    /// missing may only have failed on a damaged file, so it's kept with the data rather than deleted.
    /// </summary>
    public static string LockedKeyName(string stamp) => $"{DeviceKeyName}.locked-{stamp}";

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
            await Task.Run(() =>
            {
                SqliteConnection.ClearAllPools();
                SqliteCipher.EncryptInPlace(path, key.Value, wal: true);
            });

        if (exists && !SqliteCipher.Opens(path, key.Value))
        {
            key.Unload();
            return DatabaseState.KeyMissing;
        }

        // Every launch, not only the one that encrypted the database: one interrupted part-way leaves copies behind.
        await Task.Run(EncryptLeftovers);

        await using var db = new SpendSenseDbContext(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite($"Data Source={path}")
            .AddInterceptors(new DatabaseKeyInterceptor(key))
            .Options);

        // Installing an older build over a newer one: EF would happily run, and every query on a changed table fails.
        var known = db.Database.GetMigrations().ToHashSet();
        if ((await db.Database.GetAppliedMigrationsAsync()).Any(m => !known.Contains(m)))
        {
            key.Unload();
            return DatabaseState.NewerVersion;
        }

        await db.Database.MigrateAsync();
        return DatabaseState.Ready;
    }

    /// <summary>
    /// Moves the unreadable database aside (kept, never deleted, in case the key comes back) and starts again with a
    /// new key and an empty database. The old key, if there is one, is kept too (<see cref="LockedKeyName"/>).
    /// Returns the folder the old data went to.
    /// </summary>
    public async Task<string> StartFresh()
    {
        SqliteConnection.ClearAllPools();
        var stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);
        var locked = Path.Join(DatabasePaths.Locked(fileSystem), stamp);
        Directory.CreateDirectory(locked);

        foreach (var file in DatabasePaths.WithSidecars(DatabasePaths.Database(fileSystem)).Where(File.Exists))
            File.Move(file, Path.Join(locked, Path.GetFileName(file)));

        // Safety copies were made with the old key, so they belong with the old data.
        var safetyCopies = DatabasePaths.SafetyCopies(fileSystem);
        if (Directory.Exists(safetyCopies))
            Directory.Move(safetyCopies, Path.Join(locked, "Backups"));

        if (await secrets.Get(DeviceKeyName) is { } oldKey)
            await secrets.Set(LockedKeyName(stamp), oldKey);
        await secrets.Remove(DeviceKeyName);
        key.Unload();

        if (await Open() != DatabaseState.Ready)
            throw new InvalidOperationException("A new database couldn't be created.");
        return locked;
    }

    /// <summary>
    /// Data from before encryption besides the database itself (ADR 0005 decision 3): plaintext safety copies are
    /// encrypted in place, and plaintext databases left in the export and import caches are deleted. Cheap once
    /// there are none, so it runs on every launch.
    /// </summary>
    void EncryptLeftovers()
    {
        var safetyCopies = DatabasePaths.SafetyCopies(fileSystem);
        if (Directory.Exists(safetyCopies))
        {
            foreach (var copy in Directory.GetFiles(safetyCopies, "*.db").Where(SqliteCipher.IsPlaintext))
                SqliteCipher.EncryptInPlace(copy, key.Value, wal: false);
        }

        foreach (var cache in new[] { "exports", "imports" }.Select(name => Path.Join(fileSystem.CacheDirectory, name)))
        {
            if (!Directory.Exists(cache))
                continue;
            foreach (var file in Directory.GetFiles(cache).Where(SqliteCipher.IsPlaintext))
                File.Delete(file);
        }
    }
}
