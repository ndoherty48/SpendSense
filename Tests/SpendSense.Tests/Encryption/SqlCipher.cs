using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Interceptors;

namespace SpendSense.Tests.Encryption;

/// <summary>Raw-connection helpers for the sqlite3mc spike.</summary>
static class SqlCipher
{
    /// <summary>Fixed test keys: 32 bytes each, as hex. Never real keys.</summary>
    public const string KeyA = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";
    public const string KeyB = "F0E0D0C0B0A090807060504030201000FFEEDDCCBBAA99887766554433221100";

    public const int NotADatabase = 26; // SQLITE_NOTADB

    public static string HexKey(string hex) => $"PRAGMA hexkey = '{hex}';";

    public static string HexRekey(string hex) => $"PRAGMA hexrekey = '{hex}';";

    /// <summary>The same 256-bit key as raw key material: skips the cipher's PBKDF2 (hexkey does not).</summary>
    public static string RawKey(string hex) => $"PRAGMA key = 'raw:{hex}';";

    public static bool HasSidecars(string path) => File.Exists(path + "-wal") || File.Exists(path + "-shm");

    /// <summary>A plaintext SpendSense database made the way the shipped app makes one (EF migrations, WAL).</summary>
    public static async Task<string> CreatePlaintextDatabase(string path, string description = "Groceries")
    {
        await using var db = Context(path, hexKey: null);
        await db.Database.MigrateAsync();
        db.Transactions.Add(Support.Make.Transaction(Common.Models.Enums.TransactionTypeEnum.Expense, 12.5, accountId: 1, description: description));
        await db.SaveChangesAsync();
        return path;
    }

    /// <summary>The same, encrypted with <paramref name="hexKey"/> from the first page.</summary>
    public static async Task<string> CreateEncryptedDatabase(string path, string hexKey, string description = "Groceries")
    {
        await using var db = Context(path, hexKey);
        await db.Database.MigrateAsync();
        db.Transactions.Add(Support.Make.Transaction(Common.Models.Enums.TransactionTypeEnum.Expense, 12.5, accountId: 1, description: description));
        await db.SaveChangesAsync();
        return path;
    }

    /// <summary>Opens a raw connection, applying <paramref name="hexKey"/> first when given.</summary>
    public static SqliteConnection Open(string path, string? hexKey = null, bool pooling = false, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        OpenWith(path, hexKey is null ? null : HexKey(hexKey), pooling, mode);

    /// <summary>Opens a raw connection and runs <paramref name="keyPragma"/> (any key form) first.</summary>
    public static SqliteConnection OpenWith(string path, string? keyPragma, bool pooling = false, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = pooling, Mode = mode }.ToString());
        connection.Open();
        if (keyPragma is not null)
            Exec(connection, keyPragma);
        return connection;
    }

    public static SpendSenseDbContext Context(string path, string? hexKey, bool pooling = false, HexKeyInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = pooling }.ToString())
            .AddInterceptors(new AddEntityInterceptor(), new ModifyEntityInterceptor());
        if (interceptor is not null || hexKey is not null)
            builder.AddInterceptors(interceptor ?? new HexKeyInterceptor(hexKey!));
        return new SpendSenseDbContext(builder.Options);
    }

    public static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    /// <summary>Whether the file starts with SQLite's plaintext header ("SQLite format 3\0").</summary>
    public static bool HasPlaintextHeader(string path)
    {
        var header = new byte[16];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream.Read(header, 0, 16) == 16 && System.Text.Encoding.ASCII.GetString(header) == "SQLite format 3\0";
    }

    /// <summary>The SQLite error code a query against the file raises, or 0 when it reads fine.</summary>
    public static int ErrorReading(string path, string? hexKey) => ErrorReadingWith(path, hexKey is null ? null : HexKey(hexKey));

    public static int ErrorReadingWith(string path, string? keyPragma)
    {
        try
        {
            using var connection = OpenWith(path, keyPragma);
            Scalar(connection, "SELECT COUNT(*) FROM sqlite_master;");
            return 0;
        }
        catch (SqliteException ex)
        {
            return ex.SqliteErrorCode;
        }
    }

    public static long TransactionCount(string path, string? hexKey)
    {
        using var connection = Open(path, hexKey);
        return (long)Scalar(connection, "SELECT COUNT(*) FROM Transactions;")!;
    }
}
