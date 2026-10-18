using System.Security.Cryptography;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Security;
using SpendSense.Tests.Support;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Data;

/// <summary>Opening the encrypted database at startup (ADR 0004 decisions 2–4), against real files.</summary>
public sealed class DatabaseOpenerTests : IDisposable
{
    readonly TempFileSystem files = new();
    readonly InMemorySecretStore secrets = new();
    readonly DatabaseKey key = new();

    DatabaseOpener Opener => new(secrets, files, key);

    string DatabasePath => DatabasePaths.Database(files);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    SpendSenseDbContext Context(byte[]? withKey) =>
        new(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString())
            .AddInterceptors(withKey is null ? [] : [new DatabaseKeyInterceptor(Loaded(withKey))])
            .Options);

    static DatabaseKey Loaded(byte[] value)
    {
        var k = new DatabaseKey();
        k.Load(value);
        return k;
    }

    async Task<byte[]> StoredKey() => (await secrets.Get(DatabaseOpener.DeviceKeyName))!;

    /// <summary>A plaintext database as the app made it before ADR 0004: migrated by EF, in WAL mode.</summary>
    async Task CreatePlaintextDatabase(string description)
    {
        Directory.CreateDirectory(DatabasePaths.Folder(files));
        await using var db = Context(withKey: null);
        await db.Database.MigrateAsync(Ct);
        db.Transactions.Add(Make.Transaction(TransactionTypeEnum.Expense, 12.5, accountId: 1, description: description));
        await db.SaveChangesAsync(Ct);
    }

    async Task<List<string>> Descriptions(byte[] withKey)
    {
        await using var db = Context(withKey);
        return await db.Transactions.Select(t => t.Description).ToListAsync(Ct);
    }

    [Fact]
    public async Task A_new_install_creates_a_key_and_an_encrypted_migrated_database()
    {
        Assert.Equal(DatabaseState.Ready, await Opener.Open());

        var stored = await StoredKey();
        Assert.Equal(DatabaseKey.Size, stored.Length);
        Assert.Equal(stored, key.Value);
        Assert.False(SqliteCipher.IsPlaintext(DatabasePath));
        Assert.False(SqliteCipher.Opens(DatabasePath, key: null));
        await using var db = Context(stored);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
    }

    [Fact]
    public async Task The_next_launch_opens_the_same_data_with_the_stored_key()
    {
        await Opener.Open();
        await using (var db = Context(key.Value))
        {
            db.Transactions.Add(Make.Transaction(TransactionTypeEnum.Expense, 5, accountId: 1, description: "Kept"));
            await db.SaveChangesAsync(Ct);
        }

        var nextLaunch = new DatabaseKey();
        Assert.Equal(DatabaseState.Ready, await new DatabaseOpener(secrets, files, nextLaunch).Open());

        Assert.Equal(["Kept"], await Descriptions(nextLaunch.Value));
    }

    [Fact]
    public async Task Data_from_before_encryption_is_encrypted_in_place_on_the_first_launch()
    {
        await CreatePlaintextDatabase("From before");
        var safetyCopy = Path.Join(DatabasePaths.SafetyCopies(files), "before-restore-2026-10-01-120000.db");
        Directory.CreateDirectory(DatabasePaths.SafetyCopies(files));
        File.Copy(DatabasePath, safetyCopy);
        var oldExport = Path.Join(files.CacheDirectory, "exports", "SpendSense-backup-2026-10-01-1200.db");
        Directory.CreateDirectory(Path.GetDirectoryName(oldExport)!);
        File.Copy(DatabasePath, oldExport);
        SqliteConnection.ClearAllPools();

        Assert.Equal(DatabaseState.Ready, await Opener.Open());

        Assert.False(SqliteCipher.IsPlaintext(DatabasePath));
        Assert.Equal(["From before"], await Descriptions(key.Value));
        Assert.False(SqliteCipher.IsPlaintext(safetyCopy));
        Assert.True(SqliteCipher.Opens(safetyCopy, key.Value));
        Assert.False(File.Exists(oldExport));

        using var connection = SqliteCipher.Open(DatabasePath, key.Value);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", command.ExecuteScalar());
    }

    [Fact]
    public async Task A_conversion_interrupted_after_the_key_was_stored_finishes_on_the_next_launch()
    {
        await CreatePlaintextDatabase("Half converted");
        await secrets.Set(DatabaseOpener.DeviceKeyName, RandomNumberGenerator.GetBytes(DatabaseKey.Size));
        SqliteConnection.ClearAllPools();

        Assert.Equal(DatabaseState.Ready, await Opener.Open());

        Assert.False(SqliteCipher.IsPlaintext(DatabasePath));
        Assert.Equal(["Half converted"], await Descriptions(await StoredKey()));
    }

    [Fact]
    public async Task An_encrypted_database_without_its_key_needs_recovering()
    {
        await Opener.Open();
        SqliteConnection.ClearAllPools();
        await secrets.Remove(DatabaseOpener.DeviceKeyName);

        var reopened = new DatabaseKey();
        Assert.Equal(DatabaseState.KeyMissing, await new DatabaseOpener(secrets, files, reopened).Open());

        Assert.False(reopened.IsLoaded);
        Assert.Null(await secrets.Get(DatabaseOpener.DeviceKeyName));
    }

    [Fact]
    public async Task A_key_that_does_not_open_the_database_needs_recovering()
    {
        await Opener.Open();
        SqliteConnection.ClearAllPools();
        await secrets.Set(DatabaseOpener.DeviceKeyName, RandomNumberGenerator.GetBytes(DatabaseKey.Size));

        var reopened = new DatabaseKey();
        Assert.Equal(DatabaseState.KeyMissing, await new DatabaseOpener(secrets, files, reopened).Open());
        Assert.False(reopened.IsLoaded);
    }

    [Fact]
    public async Task Starting_fresh_keeps_the_locked_data_and_opens_an_empty_database_with_a_new_key()
    {
        await Opener.Open();
        var oldKey = key.Value;
        await using (var db = Context(oldKey))
        {
            db.Transactions.Add(Make.Transaction(TransactionTypeEnum.Expense, 5, accountId: 1, description: "Locked away"));
            await db.SaveChangesAsync(Ct);
        }
        SqliteConnection.ClearAllPools();
        await secrets.Remove(DatabaseOpener.DeviceKeyName);
        Assert.Equal(DatabaseState.KeyMissing, await Opener.Open());

        var locked = await Opener.StartFresh();

        Assert.StartsWith(DatabasePaths.Locked(files), locked);
        var lockedDatabase = Path.Join(locked, "SpendSense.db");
        Assert.True(SqliteCipher.Opens(lockedDatabase, oldKey));
        Assert.NotEqual(oldKey, key.Value);
        Assert.Equal(key.Value, await StoredKey());
        Assert.Empty(await Descriptions(key.Value));
    }

    [Fact]
    public async Task An_empty_file_left_by_sqlite_counts_as_no_database()
    {
        Directory.CreateDirectory(DatabasePaths.Folder(files));
        await File.WriteAllBytesAsync(DatabasePath, [], Ct);

        Assert.Equal(DatabaseState.Ready, await Opener.Open());
        Assert.False(SqliteCipher.IsPlaintext(DatabasePath));
    }

    public void Dispose() => files.Dispose();
}
