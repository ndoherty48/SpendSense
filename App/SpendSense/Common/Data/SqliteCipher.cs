using System.Text;

using Microsoft.Data.Sqlite;

namespace SpendSense.Common.Data;

/// <summary>
/// SQLite3 Multiple Ciphers operations outside EF (ADR 0004, <c>docs/spikes/sqlite3mc.md</c>). Keys are raw 256-bit
/// key material (<c>'raw:&lt;hex&gt;'</c>), which skips the cipher's passphrase KDF.
/// </summary>
public static class SqliteCipher
{
    /// <summary>SQLITE_NOTADB: what a wrong key, a missing key or a non-database file raises on the first read.</summary>
    public const int NotADatabase = 26;

    static readonly byte[] PlaintextHeader = "SQLite format 3\0"u8.ToArray();

    public static string KeyPragma(byte[] key) => $"PRAGMA key = 'raw:{Convert.ToHexString(key)}';";

    static string RekeyPragma(byte[] key) => $"PRAGMA rekey = 'raw:{Convert.ToHexString(key)}';";

    /// <summary>Whether the file starts with SQLite's plaintext header. An encrypted database never does.</summary>
    public static bool IsPlaintext(string path)
    {
        Span<byte> header = stackalloc byte[16];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream.ReadAtLeast(header, 16, throwOnEndOfStream: false) == 16 && header.SequenceEqual(PlaintextHeader);
    }

    /// <summary>
    /// Opens an unpooled connection, applying <paramref name="key"/> first when given. Unpooled, so it never gets a
    /// handle that another connection already keyed.
    /// </summary>
    public static SqliteConnection Open(string path, byte[]? key, bool readOnly = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        if (key is not null)
            Execute(connection, KeyPragma(key));
        return connection;
    }

    /// <summary>Whether <paramref name="key"/> opens the database. The key pragma never fails; the first read does.</summary>
    public static bool Opens(string path, byte[]? key)
    {
        try
        {
            using var connection = Open(path, key, readOnly: true);
            Execute(connection, "SELECT count(*) FROM sqlite_master;");
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == NotADatabase)
        {
            return false;
        }
    }

    /// <summary>
    /// Encrypts a plaintext database in place. sqlite3mc won't rekey a plaintext database in WAL mode, so it's
    /// switched to a rollback journal first; <paramref name="wal"/> switches it back afterwards. No other connection
    /// may hold the file: clear the pool first.
    /// </summary>
    public static void EncryptInPlace(string path, byte[] key, bool wal)
    {
        using var connection = Open(path, key: null);
        Execute(connection, "PRAGMA journal_mode = DELETE;");
        Execute(connection, RekeyPragma(key));
        if (wal)
            Execute(connection, "PRAGMA journal_mode = WAL;");
    }

    /// <summary>
    /// Writes a consistent copy of the database at <paramref name="source"/> to a new file encrypted with
    /// <paramref name="targetKey"/>. Unlike the backup API, this works from a plaintext source.
    /// </summary>
    public static void CopyEncrypted(string source, byte[]? sourceKey, string target, byte[] targetKey)
    {
        if (File.Exists(target))
            File.Delete(target);
        using var connection = Open(source, sourceKey, readOnly: true);
        var uri = $"{new Uri(Path.GetFullPath(target)).AbsoluteUri}?key=raw:{Convert.ToHexString(targetKey)}";
        Execute(connection, $"VACUUM INTO '{uri.Replace("'", "''")}';");
    }

    public static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
