using System.Diagnostics;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Tests.TestDoubles;

using static SpendSense.Tests.Encryption.SqlCipher;

namespace SpendSense.Tests.Encryption;

/// <summary>
/// Spike (docs/spikes/sqlite3mc.md): what SQLite3 Multiple Ciphers does with SpendSense's database, its in-place
/// conversion from plaintext, the backup API that <c>BackupService</c> uses, and connection pooling. Every database
/// here is a real temporary file. Runs on its own: other classes clear the process-wide connection pool, which
/// would break the pooling tests and skew the timings.
/// </summary>
[Collection(nameof(Sqlite3McSpikeTests))]
public sealed class Sqlite3McSpikeTests : IDisposable
{
    readonly TempFileSystem files = new();

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static void Report(string line)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(line);
        // SPIKE_LOG=<file> collects the numbers quoted in docs/spikes/sqlite3mc.md.
        if (Environment.GetEnvironmentVariable("SPIKE_LOG") is { Length: > 0 } log)
            File.AppendAllText(log, line + Environment.NewLine);
    }

    string PathFor(string name) => files.PathFor(name);

    public void Dispose() => files.Dispose();

    [Fact]
    public void The_engine_is_sqlite3mc()
    {
        using var connection = Open(PathFor("version.db"));

        Assert.StartsWith("SQLite3 Multiple Ciphers 2.4.0", (string)Scalar(connection, "SELECT sqlite3mc_version();")!);
        Assert.Equal("chacha20", Scalar(connection, "PRAGMA cipher;"));
    }

    // (a) Wrong key / right key

    [Fact]
    public async Task EF_creates_an_encrypted_database_in_WAL_mode()
    {
        var path = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);

        Assert.False(HasPlaintextHeader(path));
        using var connection = Open(path, KeyA);
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode;"));
        Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check;"));
    }

    [Fact]
    public async Task A_wrong_key_or_no_key_fails_with_SQLITE_NOTADB_and_the_right_key_opens()
    {
        var path = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);

        Assert.Equal(NotADatabase, ErrorReading(path, KeyB));
        Assert.Equal(NotADatabase, ErrorReading(path, hexKey: null));
        Assert.Equal(1, TransactionCount(path, KeyA));

        // Through EF: the key pragma itself succeeds; the first real statement is what fails.
        await using var wrong = Context(path, KeyB);
        var ex = await Assert.ThrowsAsync<SqliteException>(() => wrong.Transactions.CountAsync(Ct));
        Assert.Equal(NotADatabase, ex.SqliteErrorCode);

        await using var right = Context(path, KeyA);
        Assert.Equal("Groceries", (await right.Transactions.SingleAsync(Ct)).Description);
    }

    // (b) Converting an existing plaintext database

    [Fact]
    public async Task EF_leaves_no_wal_or_shm_once_its_last_unpooled_connection_closes()
    {
        var path = await CreatePlaintextDatabase(PathFor("plain.db"));

        Assert.True(HasPlaintextHeader(path));
        Assert.False(HasSidecars(path));
        using var connection = Open(path);
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode;"));
    }

    [Fact]
    public async Task Rekey_in_place_after_switching_to_DELETE_journal_encrypts_the_file()
    {
        var path = await CreatePlaintextDatabase(PathFor("plain.db"));

        using (var connection = Open(path))
        {
            Assert.Equal("delete", Scalar(connection, "PRAGMA journal_mode = DELETE;"));
            Assert.False(HasSidecars(path));
            Exec(connection, HexRekey(KeyA));
        }

        Assert.False(HasPlaintextHeader(path));
        Assert.False(HasSidecars(path));
        Assert.Equal(NotADatabase, ErrorReading(path, hexKey: null));
        Assert.Equal(1, TransactionCount(path, KeyA));

        // WAL can be switched back on afterwards; it works with the cipher.
        using (var connection = Open(path, KeyA))
        {
            Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode = WAL;"));
            Exec(connection, "INSERT INTO Tags (Name, CreatedAt, UpdatedAt) VALUES ('after', '2026-10-17', '2026-10-17');");
            Assert.True(File.Exists(path + "-wal"));
        }
        Assert.Equal(0, ErrorReading(path, KeyA));
        Assert.Equal(NotADatabase, ErrorReading(path, hexKey: null));
    }

    [Fact]
    public async Task Rekeying_a_plaintext_database_in_WAL_mode_is_refused()
    {
        var path = await CreatePlaintextDatabase(PathFor("plain.db"));

        using (var connection = Open(path))
        {
            Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode;"));
            var ex = Assert.Throws<SqliteException>(() => Exec(connection, HexRekey(KeyA)));
            Assert.Equal(1, ex.SqliteErrorCode);
            Assert.Contains("not supported in WAL journal mode", ex.Message);
        }

        Assert.True(HasPlaintextHeader(path));
        Assert.Equal(1, TransactionCount(path, hexKey: null));
    }

    /// <summary>
    /// sqlite3mc only notices WAL mode once the pager has read the file. As the very first statement on a connection
    /// the rekey slips past that check and is written through the WAL. It happens to produce a sound database here,
    /// but it's the path sqlite3mc says is unsupported, so the real conversion must switch to DELETE first.
    /// </summary>
    [Fact]
    public async Task Rekey_as_the_first_statement_slips_past_the_WAL_check()
    {
        var path = await CreatePlaintextDatabase(PathFor("plain.db"));
        var size = new FileInfo(path).Length;

        using (var connection = Open(path))
        {
            Exec(connection, HexRekey(KeyA));

            // Until the checkpoint the main file is still plaintext and the re-encrypted pages are all in the -wal.
            Assert.True(HasPlaintextHeader(path));
            Assert.True(new FileInfo(path + "-wal").Length >= size * 9 / 10);
        }

        Assert.False(HasSidecars(path));
        Assert.False(HasPlaintextHeader(path));
        using var reopened = Open(path, KeyA);
        Assert.Equal("wal", Scalar(reopened, "PRAGMA journal_mode;"));
        Assert.Equal("ok", Scalar(reopened, "PRAGMA integrity_check;"));
        Assert.Equal(1L, Scalar(reopened, "SELECT COUNT(*) FROM Transactions;"));
    }

    [Fact]
    public async Task Switching_journal_mode_is_locked_out_while_a_pooled_connection_is_held()
    {
        var path = await CreatePlaintextDatabase(PathFor("plain.db"));
        using (var pooled = Open(path, pooling: true))
            Exec(pooled, "INSERT INTO Tags (Name, CreatedAt, UpdatedAt) VALUES ('held', '2026-10-17', '2026-10-17');");

        // The closed connection is still open natively, in the pool, holding the WAL.
        Assert.True(File.Exists(path + "-wal"));
        using (var connection = Open(path))
        {
            var ex = Assert.Throws<SqliteException>(() => Scalar(connection, "PRAGMA journal_mode = DELETE;"));
            Assert.Equal(5, ex.SqliteErrorCode); // SQLITE_BUSY
        }

        SqliteConnection.ClearAllPools();
        using var cleared = Open(path);
        Assert.Equal("delete", Scalar(cleared, "PRAGMA journal_mode = DELETE;"));
    }

    [Fact]
    public async Task VACUUM_INTO_a_keyed_uri_writes_an_encrypted_copy_and_leaves_the_source_alone()
    {
        var source = await CreatePlaintextDatabase(PathFor("plain.db"));
        var hexTarget = PathFor("hex.db");
        var rawTarget = PathFor("raw.db");

        using (var connection = Open(source, mode: SqliteOpenMode.ReadOnly))
        {
            Exec(connection, $"VACUUM INTO 'file:{hexTarget}?hexkey={KeyA}';");
            Exec(connection, $"VACUUM INTO 'file:{rawTarget}?key=raw:{KeyA}';");
        }

        Assert.True(HasPlaintextHeader(source));
        Assert.Equal(1, TransactionCount(source, hexKey: null));

        Assert.False(HasPlaintextHeader(hexTarget));
        Assert.Equal(1, TransactionCount(hexTarget, KeyA));
        Assert.Equal(NotADatabase, ErrorReading(hexTarget, hexKey: null));

        // hexkey and raw: are different keys for the same bytes: hexkey runs PBKDF2 over them, raw: uses them as is.
        Assert.Equal(0, ErrorReadingWith(rawTarget, RawKey(KeyA)));
        Assert.Equal(NotADatabase, ErrorReading(rawTarget, KeyA));

        // A VACUUM INTO target is created in DELETE mode, whatever the source used.
        using var copy = Open(hexTarget, KeyA);
        Assert.Equal("delete", Scalar(copy, "PRAGMA journal_mode;"));
    }

    [Fact]
    public async Task VACUUM_INTO_a_plain_path_copies_the_source_cipher()
    {
        var plain = await CreatePlaintextDatabase(PathFor("plain.db"));
        var encrypted = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);
        var fromPlain = PathFor("from-plain.db");
        var fromEncrypted = PathFor("from-encrypted.db");

        using (var connection = Open(plain))
            Exec(connection, $"VACUUM INTO '{fromPlain}';");
        using (var connection = Open(encrypted, KeyA))
            Exec(connection, $"VACUUM INTO '{fromEncrypted}';");

        Assert.True(HasPlaintextHeader(fromPlain));
        Assert.Equal(1, TransactionCount(fromEncrypted, KeyA));
        Assert.Equal(NotADatabase, ErrorReading(fromEncrypted, hexKey: null));
    }

    // (c) SqliteConnection.BackupDatabase, as BackupService uses it

    [Fact]
    public async Task Backup_API_copies_an_encrypted_database_to_a_target_with_the_same_key()
    {
        var live = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);
        var target = PathFor("backup.db");

        using (var source = Open(live, KeyA))
        using (var destination = Open(target, KeyA))
            source.BackupDatabase(destination);

        Assert.False(HasPlaintextHeader(target));
        Assert.Equal(1, TransactionCount(target, KeyA));
        Assert.Equal(NotADatabase, ErrorReading(target, KeyB));
    }

    [Fact]
    public async Task Backup_API_re_encrypts_pages_for_a_target_with_a_different_key()
    {
        var live = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);
        var target = PathFor("backup.db");

        using (var source = Open(live, KeyA))
        using (var destination = Open(target, KeyB))
            source.BackupDatabase(destination);

        Assert.Equal(1, TransactionCount(target, KeyB));
        Assert.Equal(NotADatabase, ErrorReading(target, KeyA));
        Assert.Equal(NotADatabase, ErrorReading(target, hexKey: null));
    }

    [Fact]
    public async Task Backup_API_refuses_an_encrypted_source_into_a_plaintext_target()
    {
        var live = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);

        using var source = Open(live, KeyA);
        using var destination = Open(PathFor("plain-backup.db"));
        var ex = Assert.Throws<SqliteException>(() => source.BackupDatabase(destination));

        Assert.Equal(1, ex.SqliteErrorCode);
        Assert.Contains("incompatible source and target", ex.Message);
    }

    [Fact]
    public async Task Backup_API_refuses_an_old_plaintext_backup_into_the_encrypted_live_database()
    {
        var oldBackup = await CreatePlaintextDatabase(PathFor("old-backup.db"), description: "From the old backup");
        var live = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);

        // As BackupService.Restore does it: the backup opened read-only, copied over the live connection.
        using (var source = Open(oldBackup, mode: SqliteOpenMode.ReadOnly))
        using (var destination = Open(live, KeyA))
        {
            var ex = Assert.Throws<SqliteException>(() => source.BackupDatabase(destination));
            Assert.Equal(1, ex.SqliteErrorCode);
        }

        // Nothing was overwritten.
        await using var db = Context(live, KeyA);
        Assert.Equal("Groceries", (await db.Transactions.SingleAsync(Ct)).Description);
    }

    [Fact]
    public async Task An_old_plaintext_backup_restores_through_a_keyed_VACUUM_INTO_staging_copy()
    {
        var oldBackup = await CreatePlaintextDatabase(PathFor("old-backup.db"), description: "From the old backup");
        var live = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);
        var staging = PathFor("staging.db");

        using (var source = Open(oldBackup, mode: SqliteOpenMode.ReadOnly))
            Exec(source, $"VACUUM INTO 'file:{staging}?hexkey={KeyA}';");
        using (var source = Open(staging, KeyA))
        using (var destination = Open(live, KeyA))
            source.BackupDatabase(destination);

        Assert.False(HasPlaintextHeader(live));
        Assert.Equal(NotADatabase, ErrorReading(live, hexKey: null));
        await using var db = Context(live, KeyA);
        Assert.Equal("From the old backup", (await db.Transactions.SingleAsync(Ct)).Description);
        using var check = Open(live, KeyA);
        Assert.Equal("wal", Scalar(check, "PRAGMA journal_mode;"));
    }

    [Fact]
    public async Task ATTACH_inherits_the_main_key_unless_given_its_own()
    {
        var oldBackup = await CreatePlaintextDatabase(PathFor("old-backup.db"));
        var live = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);

        using (var connection = Open(live, KeyA))
        {
            var ex = Assert.Throws<SqliteException>(() => Exec(connection, $"ATTACH DATABASE '{oldBackup}' AS old;"));
            Assert.Equal(NotADatabase, ex.SqliteErrorCode);
        }

        using var plainAttach = Open(live, KeyA);
        Exec(plainAttach, $"ATTACH DATABASE '{oldBackup}' AS old KEY '';");
        Assert.Equal(1L, Scalar(plainAttach, "SELECT COUNT(*) FROM old.Transactions;"));
    }

    // (d) Connection pooling and per-open cost

    [Fact]
    public async Task With_pooling_the_interceptor_rekeys_every_open_and_a_reused_handle_still_needs_the_right_key()
    {
        var path = await CreateEncryptedDatabase(PathFor("live.db"), KeyA);
        var interceptor = new HexKeyInterceptor(KeyA);

        for (var i = 0; i < 5; i++)
        {
            await using var db = Context(path, hexKey: null, pooling: true, interceptor: interceptor);
            Assert.Equal(1, await db.Transactions.CountAsync(Ct));
        }

        // ConnectionOpened fires on every logical open, even when the pool hands back a keyed native handle.
        Assert.Equal(5, interceptor.Applied);

        // A pooled native handle keeps its key: an open with no key pragma at all reuses it and reads fine. So with
        // pooling on, the pool (not the interceptor) is what decides whether a connection is keyed.
        SqliteConnection.ClearAllPools();
        using (var right = Open(path, KeyA, pooling: true))
            Scalar(right, "SELECT COUNT(*) FROM Transactions;");
        using (var noKey = Open(path, hexKey: null, pooling: true))
            Assert.Equal(1L, Scalar(noKey, "SELECT COUNT(*) FROM Transactions;"));

        // Re-keying a pooled handle with the wrong key replaces its key: it is locked from then on.
        using (var wrong = Open(path, KeyB, pooling: true))
            Assert.Equal(NotADatabase, Assert.Throws<SqliteException>(() => Scalar(wrong, "SELECT COUNT(*) FROM Transactions;")).SqliteErrorCode);
        using (var afterWrong = Open(path, hexKey: null, pooling: true))
            Assert.Equal(NotADatabase, Assert.Throws<SqliteException>(() => Scalar(afterWrong, "SELECT COUNT(*) FROM Transactions;")).SqliteErrorCode);
        using (var recovered = Open(path, KeyA, pooling: true))
            Assert.Equal(1L, Scalar(recovered, "SELECT COUNT(*) FROM Transactions;"));
    }

    [Fact]
    public async Task Hexkey_runs_the_KDF_on_every_open_and_a_raw_key_does_not()
    {
        var hexPath = await CreateEncryptedDatabase(PathFor("hex.db"), KeyA);
        var rawPath = PathFor("raw.db");
        using (var connection = OpenWith(rawPath, RawKey(KeyA)))
            Exec(connection, "CREATE TABLE t (x); INSERT INTO t VALUES (1);");
        var plainPath = await CreatePlaintextDatabase(PathFor("plain.db"));

        double PerOpen(string path, string? pragma, bool pooling, string table = "Transactions")
        {
            using (var warm = OpenWith(path, pragma, pooling))
                Scalar(warm, $"SELECT COUNT(*) FROM {table};");
            const int opens = 20;
            var clock = Stopwatch.StartNew();
            for (var i = 0; i < opens; i++)
            {
                using var connection = OpenWith(path, pragma, pooling);
                Scalar(connection, $"SELECT COUNT(*) FROM {table};");
            }
            return clock.Elapsed.TotalMilliseconds / opens;
        }

        var hexUnpooled = PerOpen(hexPath, HexKey(KeyA), pooling: false);
        var hexPooled = PerOpen(hexPath, HexKey(KeyA), pooling: true);
        var rawUnpooled = PerOpen(rawPath, RawKey(KeyA), pooling: false, table: "t");
        var rawPooled = PerOpen(rawPath, RawKey(KeyA), pooling: true, table: "t");
        var plainUnpooled = PerOpen(plainPath, null, pooling: false);
        var plainPooled = PerOpen(plainPath, null, pooling: true);

        Report($"hexkey: {hexUnpooled:F2} ms/open unpooled, {hexPooled:F2} pooled");
        Report($"raw:    {rawUnpooled:F2} ms/open unpooled, {rawPooled:F2} pooled");
        Report($"plain:  {plainUnpooled:F2} ms/open unpooled, {plainPooled:F2} pooled");

        // PBKDF2 (64,007 iterations for chacha20) dwarfs everything else, pooled or not.
        Assert.True(hexPooled > 5 * rawPooled, $"hexkey pooled {hexPooled:F2} ms vs raw {rawPooled:F2} ms");
        Assert.True(hexUnpooled > 5 * rawUnpooled, $"hexkey unpooled {hexUnpooled:F2} ms vs raw {rawUnpooled:F2} ms");
    }

    [Fact]
    public void The_Password_keyword_keys_only_new_native_handles_so_pooling_hides_the_KDF()
    {
        var path = PathFor("password.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Password = "a passphrase", Pooling = true }.ToString();
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            Exec(connection, "CREATE TABLE t (x);");
        }

        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            Scalar(connection, "SELECT COUNT(*) FROM t;");
        }
        var perOpen = clock.Elapsed.TotalMilliseconds / 20;
        Report($"Password= pooled: {perOpen:F2} ms/open");

        Assert.True(perOpen < 5, $"{perOpen:F2} ms/open");
        Assert.Equal(NotADatabase, ErrorReading(path, hexKey: null));
    }
}

[CollectionDefinition(nameof(Sqlite3McSpikeTests), DisableParallelization = true)]
public sealed class Sqlite3McSpikeCollection;
