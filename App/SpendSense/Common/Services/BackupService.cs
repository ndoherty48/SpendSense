using System.Globalization;
using System.Text;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;

namespace SpendSense.Common.Services;

/// <summary>What a backup file contains, or why it can't be restored.</summary>
public sealed record BackupInfo(
    bool IsValid,
    string? Problem,
    int Transactions = 0,
    int Accounts = 0,
    DateTime? LatestTransaction = null,
    bool NeedsUpgrade = false)
{
    public static BackupInfo Invalid(string problem) => new(false, problem);
}

/// <summary>
/// Backs up the whole database to a file, restores one, and exports transactions as CSV (ADR 0003).
/// Files go to the cache directory and are handed to the share sheet; nothing is kept here long-term.
/// </summary>
public class BackupService(SpendSenseDbContext db, IFileSystem fileSystem, SettingsService settings)
{
    /// <summary>Days without a backup before the dashboard nudges.</summary>
    public const int ReminderDays = 30;

    const int SafetyCopiesKept = 3;

    const string NotABackup = "This file isn't a SpendSense backup.";

    string ExportDirectory => Path.Combine(fileSystem.CacheDirectory, "exports");

    /// <summary>Where the pre-restore safety copies of the current database are kept.</summary>
    public string SafetyCopyDirectory => Path.Combine(fileSystem.AppDataDirectory, "SpendSense", "Backups");

    /// <summary>Copies the live database to a new backup file and returns its path.</summary>
    public async Task<string> CreateBackup(DateTime? now = null)
    {
        var path = NewExportFile($"SpendSense-backup-{Stamp(now)}.db");
        await CopyLiveDatabaseTo(path);
        return path;
    }

    /// <summary>Recorded once the share sheet closes after a backup.</summary>
    public void MarkBackedUp(DateTime? when = null) => settings.LastBackupAt = when ?? DateTime.Now;

    /// <summary>Writes every transaction to a CSV file and returns its path.</summary>
    public async Task<string> ExportTransactionsCsv(DateTime? now = null)
    {
        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Include(t => t.ToAccount)
            .Include(t => t.Currency)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync();

        var path = NewExportFile($"SpendSense-transactions-{(now ?? DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
        // UTF-8 with a BOM so Excel reads £ and accents correctly.
        await using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        TransactionCsv.Write(transactions, writer);
        return path;
    }

    /// <summary>Checks a file before restoring it: is it a SpendSense database this version can read?</summary>
    public BackupInfo Inspect(string path)
    {
        if (!File.Exists(path))
            return BackupInfo.Invalid("The file couldn't be read.");

        try
        {
            using var connection = OpenReadOnly(path);

            if (Scalar<string>(connection, "PRAGMA quick_check;") != "ok")
                return BackupInfo.Invalid("This backup is damaged and can't be restored.");

            var tables = Strings(connection, "SELECT name FROM sqlite_master WHERE type = 'table';").ToHashSet();
            if (!tables.Contains("__EFMigrationsHistory") || !tables.Contains("Transactions"))
                return BackupInfo.Invalid(NotABackup);

            var applied = Strings(connection, "SELECT MigrationId FROM __EFMigrationsHistory;").ToList();
            var known = db.Database.GetMigrations().ToHashSet();
            if (applied.Any(m => !known.Contains(m)))
                return BackupInfo.Invalid("This backup was made by a newer version of SpendSense. Update the app, then restore it.");

            var latest = Scalar<string>(connection, "SELECT MAX(TransactionDate) FROM Transactions;");
            return new BackupInfo(
                IsValid: true,
                Problem: null,
                Transactions: (int)Scalar<long>(connection, "SELECT COUNT(*) FROM Transactions;"),
                Accounts: tables.Contains("Accounts") ? (int)Scalar<long>(connection, "SELECT COUNT(*) FROM Accounts;") : 0,
                LatestTransaction: DateTime.TryParse(latest, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
                NeedsUpgrade: known.Any(m => !applied.Contains(m)));
        }
        catch (SqliteException)
        {
            return BackupInfo.Invalid(NotABackup);
        }
    }

    /// <summary>
    /// Replaces the current data with the backup at <paramref name="path"/>. The current database is saved as a
    /// safety copy first, and an older backup is upgraded by running the migrations.
    /// </summary>
    public async Task Restore(string path)
    {
        var info = Inspect(path);
        if (!info.IsValid)
            throw new InvalidOperationException(info.Problem);

        await SaveSafetyCopy();

        await db.Database.OpenConnectionAsync();
        try
        {
            using var source = OpenReadOnly(path);
            source.BackupDatabase((SqliteConnection)db.Database.GetDbConnection());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        // Entities tracked before the restore describe data that no longer exists.
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
    }

    /// <summary>Whether the dashboard should suggest a backup.</summary>
    public async Task<bool> NeedsReminder(DateTime? now = null) =>
        ShouldRemind(now ?? DateTime.Now, settings.LastBackupAt, settings.BackupReminderSnoozedUntil, await db.Transactions.AnyAsync());

    public static bool ShouldRemind(DateTime now, DateTime? lastBackup, DateTime? snoozedUntil, bool hasData) =>
        hasData
        && (snoozedUntil is null || now >= snoozedUntil)
        && (lastBackup is null || now - lastBackup.Value > TimeSpan.FromDays(ReminderDays));

    /// <summary>"Not now": hide the reminder for another <see cref="ReminderDays"/> days.</summary>
    public void SnoozeReminder(DateTime? now = null) =>
        settings.BackupReminderSnoozedUntil = (now ?? DateTime.Now).AddDays(ReminderDays);

    async Task SaveSafetyCopy()
    {
        Directory.CreateDirectory(SafetyCopyDirectory);
        await CopyLiveDatabaseTo(Path.Combine(SafetyCopyDirectory, $"before-restore-{Stamp(null)}.db"));

        foreach (var old in Directory.GetFiles(SafetyCopyDirectory, "before-restore-*.db").OrderDescending().Skip(SafetyCopiesKept))
            File.Delete(old);
    }

    async Task CopyLiveDatabaseTo(string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        await db.Database.OpenConnectionAsync();
        try
        {
            // No pooling: the file handle must be released before the file is shared.
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            target.Open();
            ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(target);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    string NewExportFile(string name)
    {
        // Only the latest export is needed; clear earlier ones so the cache doesn't grow.
        Directory.CreateDirectory(ExportDirectory);
        foreach (var old in Directory.GetFiles(ExportDirectory))
            File.Delete(old);
        return Path.Combine(ExportDirectory, name);
    }

    // Seconds included so two backups in the same minute (e.g. tests, double taps) don't collide.
    static string Stamp(DateTime? now) => (now ?? DateTime.Now).ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);

    static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is null or DBNull ? default! : (T)value;
    }

    static IEnumerable<string> Strings(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
            values.Add(reader.GetString(0));
        return values;
    }
}
