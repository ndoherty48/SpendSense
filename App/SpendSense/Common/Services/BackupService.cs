using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Security;

namespace SpendSense.Common.Services;

/// <summary>What a backup file contains, or why it can't be restored.</summary>
public sealed record BackupInfo(
    bool IsValid,
    string? Problem,
    int Transactions = 0,
    int Accounts = 0,
    DateTime? LatestTransaction = null,
    bool NeedsUpgrade = false,
    bool IsLocked = false)
{
    public static BackupInfo Invalid(string problem) => new(false, problem);

    /// <summary>An encrypted backup this device can't open by itself: ask for its password or recovery code.</summary>
    public static BackupInfo Locked(string? problem = null) => new(false, problem, IsLocked: true);

    /// <summary>The unwrapped key of an encrypted backup; null for a plaintext backup from before ADR 0004.</summary>
    internal byte[]? Key { get; init; }
}

/// <summary>
/// Backs up the whole database to a file, restores one, and exports transactions as CSV (ADRs 0003 and 0004).
/// A backup is a zip: a manifest, and the database encrypted with the backup's own key, which the backup password
/// and recovery code unwrap. Files go to the cache directory and are handed to the share sheet; nothing is kept
/// here long-term.
/// </summary>
public class BackupService(SpendSenseDbContext db, IFileSystem fileSystem, SettingsService settings, DatabaseKey deviceKey, BackupKeys keys)
{
    /// <summary>Days without a backup before the dashboard nudges.</summary>
    public const int ReminderDays = 30;

    const int SafetyCopiesKept = 3;

    const string NotABackup = "This file isn't a SpendSense backup.";

    const string Damaged = "This backup is damaged and can't be restored.";

    const string DatabaseEntry = "SpendSense.db";

    string ExportDirectory => Path.Combine(fileSystem.CacheDirectory, "exports");

    string StagingDirectory => Path.Combine(fileSystem.CacheDirectory, "staging");

    /// <summary>Where the pre-restore safety copies of the current database are kept.</summary>
    public string SafetyCopyDirectory => DatabasePaths.SafetyCopies(fileSystem);

    /// <summary>Whether a backup password has been set; backing up needs one.</summary>
    public Task<bool> HasPassword() => keys.IsSetUp();

    /// <summary>Encrypts a copy of the live database with a new backup key, zips it with its manifest, and returns the path.</summary>
    public async Task<string> CreateBackup(DateTime? now = null)
    {
        var backupKey = RandomNumberGenerator.GetBytes(DatabaseKey.Size);
        var manifest = await keys.CreateManifest(backupKey, (now ?? DateTime.Now).ToUniversalTime());

        var path = NewExportFile($"SpendSense-backup-{Stamp(now)}.zip");
        var staging = NewStagingFile();
        try
        {
            await CopyLiveDatabaseTo(staging, backupKey);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            await using (var entry = zip.CreateEntry(BackupManifest.EntryName).Open())
                await JsonSerializer.SerializeAsync(entry, manifest, BackupManifest.Json);
            zip.CreateEntryFromFile(staging, DatabaseEntry);
        }
        finally
        {
            DeleteStaging(staging);
        }
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

    /// <summary>
    /// Checks a file before restoring it: is it a SpendSense backup this version can read, and what's in it? An
    /// encrypted backup that the keys remembered on this device don't open comes back <see cref="BackupInfo.IsLocked"/>.
    /// </summary>
    public async Task<BackupInfo> Inspect(string path)
    {
        if (!File.Exists(path))
            return BackupInfo.Invalid("The file couldn't be read.");

        // A plaintext database: a backup made before encryption (ADR 0004 decision 6).
        if (SqliteCipher.IsPlaintext(path))
            return Summarise(path, key: null);

        if (ReadManifest(path, out var manifest) is { } problem)
            return BackupInfo.Invalid(problem);
        return await keys.UnlockWithRemembered(manifest!) is { } key ? SummariseEncrypted(path, key) : BackupInfo.Locked();
    }

    /// <summary>Opens an encrypted backup with its password or recovery code, then checks it as <see cref="Inspect"/> does.</summary>
    public async Task<BackupInfo> Unlock(string path, UnlockMethod method, string secret)
    {
        if (ReadManifest(path, out var manifest) is { } problem)
            return BackupInfo.Invalid(problem);
        if (await keys.Unlock(manifest!, method, secret) is { } key)
            return SummariseEncrypted(path, key);

        return BackupInfo.Locked(method == UnlockMethod.Password
            ? "That password doesn't match this backup. Check for typos, or use the recovery code instead."
            : "That recovery code doesn't match this backup.");
    }

    /// <summary>
    /// Replaces the current data with the backup at <paramref name="path"/>. The current database is saved as a
    /// safety copy first, and an older backup is upgraded by running the migrations. Pass the
    /// <paramref name="info"/> that <see cref="Inspect"/> or <see cref="Unlock"/> returned for an encrypted backup.
    /// </summary>
    public async Task Restore(string path, BackupInfo? info = null)
    {
        info ??= await Inspect(path);
        if (!info.IsValid)
            throw new InvalidOperationException(info.Problem ?? "Unlock this backup before restoring it.");

        await SaveSafetyCopy();

        var staging = NewStagingFile();
        try
        {
            // The backup API won't copy between a plaintext and an encrypted database, so a plaintext backup is
            // first copied into an encrypted staging file.
            byte[] stagingKey;
            if (info.Key is { } backupKey)
            {
                ExtractDatabase(path, staging);
                stagingKey = backupKey;
            }
            else
            {
                SqliteCipher.CopyEncrypted(path, sourceKey: null, staging, deviceKey.Value);
                stagingKey = deviceKey.Value;
            }

            await db.Database.OpenConnectionAsync();
            try
            {
                using var source = SqliteCipher.Open(staging, stagingKey, readOnly: true);
                source.BackupDatabase((SqliteConnection)db.Database.GetDbConnection());
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
        finally
        {
            DeleteStaging(staging);
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
        await CopyLiveDatabaseTo(Path.Combine(SafetyCopyDirectory, $"before-restore-{Stamp(null)}.db"), deviceKey.Value);

        foreach (var old in Directory.GetFiles(SafetyCopyDirectory, "before-restore-*.db").OrderDescending().Skip(SafetyCopiesKept))
            File.Delete(old);
    }

    /// <summary>Copies the live database to a new file encrypted with <paramref name="key"/>; the backup API re-encrypts each page.</summary>
    async Task CopyLiveDatabaseTo(string path, byte[] key)
    {
        if (File.Exists(path))
            File.Delete(path);

        await db.Database.OpenConnectionAsync();
        try
        {
            // Unpooled: the file handle must be released before the file is shared.
            using var target = SqliteCipher.Open(path, key);
            ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(target);
            // The copy inherits the live database's WAL mode; a rollback journal keeps it a single, self-contained file.
            SqliteCipher.Execute(target, "PRAGMA journal_mode = DELETE;");
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

    static void DeleteStaging(string staging)
    {
        foreach (var file in DatabasePaths.WithSidecars(staging))
            File.Delete(file);
    }

    string NewStagingFile()
    {
        Directory.CreateDirectory(StagingDirectory);
        return Path.Combine(StagingDirectory, $"{Guid.NewGuid():N}.db");
    }

    /// <summary>Reads and checks the manifest of a backup zip. Returns the problem, or null when it's usable.</summary>
    static string? ReadManifest(string path, out BackupManifest? manifest)
    {
        manifest = null;
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.GetEntry(BackupManifest.EntryName) is not { } entry || zip.GetEntry(DatabaseEntry) is null)
                return NotABackup;
            using (var stream = entry.Open())
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream, BackupManifest.Json);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            return NotABackup;
        }

        if (manifest?.Format != BackupManifest.FormatName)
            return NotABackup;
        if (manifest.FormatVersion > BackupManifest.CurrentFormatVersion)
            return "This backup was made by a newer version of SpendSense. Update the app, then restore it.";
        if (manifest.KeySlots is not { Count: > 0 } || manifest.Cipher != BackupManifest.CipherName)
            return Damaged;
        return null;
    }

    static void ExtractDatabase(string zipPath, string target)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        zip.GetEntry(DatabaseEntry)!.ExtractToFile(target, overwrite: true);
    }

    BackupInfo SummariseEncrypted(string path, byte[] key)
    {
        var staging = NewStagingFile();
        try
        {
            ExtractDatabase(path, staging);
            return Summarise(staging, key, damagedIfUnreadable: true) with { Key = key };
        }
        catch (InvalidDataException)
        {
            return BackupInfo.Invalid(Damaged);
        }
        finally
        {
            DeleteStaging(staging);
        }
    }

    // Seconds included so two backups in the same minute (e.g. tests, double taps) don't collide.
    static string Stamp(DateTime? now) => (now ?? DateTime.Now).ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);

    /// <summary>
    /// Checks a database file (opened with <paramref name="key"/>, or none for plaintext) and summarises it.
    /// <paramref name="damagedIfUnreadable"/>: the file came out of an unlocked backup, so failing to read it means
    /// it was altered, not that it's some other kind of file.
    /// </summary>
    BackupInfo Summarise(string path, byte[]? key, bool damagedIfUnreadable = false)
    {
        try
        {
            using var connection = SqliteCipher.Open(path, key, readOnly: true);

            if (Scalar<string>(connection, "PRAGMA quick_check;") != "ok")
                return BackupInfo.Invalid(Damaged);

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
            return BackupInfo.Invalid(damagedIfUnreadable ? Damaged : NotABackup);
        }
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
