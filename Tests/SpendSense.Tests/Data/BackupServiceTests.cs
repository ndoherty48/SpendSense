using System.IO.Compression;
using System.Text;
using System.Text.Json;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using SpendSense.Common.Data;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Security;
using SpendSense.Common.Services;
using SpendSense.Tests.Support;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Data;

/// <summary>
/// Backups are real encrypted zips (ADR 0004): written from, inspected against and restored over a live encrypted
/// database file. Plaintext backups from before encryption must still restore.
/// </summary>
public sealed class BackupServiceTests : DatabaseTest, IDisposable
{
    const string Password = "correct horse battery";

    readonly TempFileSystem files;
    readonly InMemorySecretStore secrets = new();
    readonly string recoveryCode;

    public BackupServiceTests() : this(new TempFileSystem()) { }

    BackupServiceTests(TempFileSystem files) : base(files)
    {
        this.files = files;
        recoveryCode = Keys(secrets).SetPassword(Password).GetAwaiter().GetResult();
    }

    BackupService Service => ServiceWith(secrets);

    BackupService ServiceWith(ISecretStore store) => new(Db, files, Settings, Key, Keys(store));

    static BackupKeys Keys(ISecretStore store) => new(store) { PasswordIterations = ComponentTest.TestPasswordIterations };

    /// <summary>The same app on another device: a different secret store, so nothing is remembered.</summary>
    BackupService OtherDevice => ServiceWith(new InMemorySecretStore());

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A plaintext SpendSense database, as backups were before ADR 0004.</summary>
    static SpendSenseDbContext OpenPlaintext(string path) =>
        new(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString())
            .Options);

    static string ReadEntry(string zipPath, string name)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    static byte[] ReadEntryBytes(string zipPath, string name)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        using var stream = new MemoryStream();
        zip.GetEntry(name)!.Open().CopyTo(stream);
        return stream.ToArray();
    }

    /// <summary>Rewrites the manifest of a backup with <paramref name="change"/> applied to its JSON.</summary>
    static void EditManifest(string zipPath, Func<string, string> change)
    {
        var manifest = change(ReadEntry(zipPath, BackupManifest.EntryName));
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        zip.GetEntry(BackupManifest.EntryName)!.Delete();
        using var writer = new StreamWriter(zip.CreateEntry(BackupManifest.EntryName).Open());
        writer.Write(manifest);
    }

    [Fact]
    public async Task A_backup_is_a_zip_of_a_manifest_and_an_encrypted_database()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 62.10, accountId: 1, description: "Groceries");

        var path = await Service.CreateBackup(new DateTime(2026, 10, 4, 9, 30, 0));

        Assert.Equal("SpendSense-backup-2026-10-04-093000.zip", Path.GetFileName(path));
        using (var zip = ZipFile.OpenRead(path))
            Assert.Equal(["manifest.json", "SpendSense.db"], zip.Entries.Select(e => e.FullName));

        var database = ReadEntryBytes(path, "SpendSense.db");
        Assert.NotEqual("SQLite format 3\0", Encoding.ASCII.GetString(database, 0, 16));
        Assert.DoesNotContain("Groceries", Encoding.UTF8.GetString(database));

        var manifest = ReadEntry(path, "manifest.json");
        Assert.DoesNotContain("Groceries", manifest);
        using var json = JsonDocument.Parse(manifest);
        Assert.Equal("spendsense-backup", json.RootElement.GetProperty("format").GetString());
        Assert.Equal(["password", "recoveryCode"], json.RootElement.GetProperty("keySlots").EnumerateArray().Select(s => s.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Backing_up_needs_a_backup_password()
    {
        var service = ServiceWith(new InMemorySecretStore());

        Assert.False(await service.HasPassword());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBackup());
    }

    [Fact]
    public async Task A_new_export_replaces_the_previous_one()
    {
        var first = await Service.CreateBackup(new DateTime(2026, 10, 1));
        var second = await Service.CreateBackup(new DateTime(2026, 10, 4));

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task A_backup_made_on_this_device_opens_without_asking_and_is_summarised()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, days: -3);
        await AddTransaction(TransactionTypeEnum.Income, 20, accountId: 1);
        var path = await Service.CreateBackup();

        var info = await Service.Inspect(path);

        Assert.True(info.IsValid, info.Problem);
        Assert.False(info.IsLocked);
        Assert.Equal(2, info.Transactions);
        Assert.Equal(1, info.Accounts);
        Assert.Equal(DateTime.Today, info.LatestTransaction?.Date);
        Assert.False(info.NeedsUpgrade);
    }

    [Fact]
    public async Task On_another_device_a_backup_is_locked_until_its_password_is_entered()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var path = await Service.CreateBackup();

        var locked = await OtherDevice.Inspect(path);
        Assert.True(locked.IsLocked);
        Assert.False(locked.IsValid);

        var wrong = await OtherDevice.Unlock(path, UnlockMethod.Password, "not the password");
        Assert.True(wrong.IsLocked);
        Assert.Contains("doesn't match", wrong.Problem);

        var unlocked = await OtherDevice.Unlock(path, UnlockMethod.Password, Password);
        Assert.True(unlocked.IsValid, unlocked.Problem);
        Assert.Equal(1, unlocked.Transactions);
    }

    [Fact]
    public async Task The_recovery_code_opens_a_backup_however_it_is_typed()
    {
        var path = await Service.CreateBackup();

        Assert.True((await OtherDevice.Unlock(path, UnlockMethod.RecoveryCode, recoveryCode)).IsValid);
        Assert.True((await OtherDevice.Unlock(path, UnlockMethod.RecoveryCode, recoveryCode.Replace("-", " ").ToLowerInvariant())).IsValid);
        Assert.True((await OtherDevice.Unlock(path, UnlockMethod.RecoveryCode, "AAAA-AAAA-AAAA-AAAA-AAAA-AAAA-AA")).IsLocked);
        Assert.True((await OtherDevice.Unlock(path, UnlockMethod.RecoveryCode, "too short")).IsLocked);
    }

    [Fact]
    public async Task A_new_password_opens_new_backups_and_the_old_one_still_opens_old_backups()
    {
        var before = await Service.CreateBackup(new DateTime(2026, 10, 1));
        var keptBefore = files.PathFor("before.zip");
        File.Copy(before, keptBefore);

        await Keys(secrets).SetPassword("a brand new password");
        var after = await Service.CreateBackup(new DateTime(2026, 10, 2));

        Assert.True((await OtherDevice.Unlock(keptBefore, UnlockMethod.Password, Password)).IsValid);
        Assert.True((await OtherDevice.Unlock(after, UnlockMethod.Password, "a brand new password")).IsValid);
        Assert.True((await OtherDevice.Unlock(after, UnlockMethod.Password, Password)).IsLocked);
        // This device no longer remembers the old password's key.
        Assert.True((await Service.Inspect(keptBefore)).IsLocked);
    }

    [Fact]
    public async Task Editing_the_manifest_stops_the_backup_opening()
    {
        var path = await Service.CreateBackup(new DateTime(2026, 10, 4, 9, 30, 0));
        EditManifest(path, json => json.Replace("2026-10-04", "2026-10-05"));

        Assert.True((await Service.Inspect(path)).IsLocked);
        Assert.True((await OtherDevice.Unlock(path, UnlockMethod.Password, Password)).IsLocked);
    }

    [Fact]
    public async Task A_backup_in_a_newer_format_is_refused()
    {
        var path = await Service.CreateBackup();
        EditManifest(path, json => json.Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));

        var info = await Service.Inspect(path);

        Assert.False(info.IsValid);
        Assert.Contains("newer version", info.Problem);
    }

    [Fact]
    public void A_file_that_is_not_a_database_is_refused()
    {
        var path = files.PathFor("notes.db");
        File.WriteAllText(path, "definitely not sqlite");

        var info = Service.Inspect(path).GetAwaiter().GetResult();

        Assert.False(info.IsValid);
        Assert.False(info.IsLocked);
        Assert.Equal("This file isn't a SpendSense backup.", info.Problem);
    }

    [Fact]
    public async Task A_zip_that_is_not_a_backup_is_refused()
    {
        var path = files.PathFor("photos.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            zip.CreateEntry("photo.jpg");

        Assert.Equal("This file isn't a SpendSense backup.", (await Service.Inspect(path)).Problem);
    }

    [Fact]
    public async Task Another_apps_database_is_refused()
    {
        var path = files.PathFor("other.db");
        using (var other = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            other.Open();
            using var command = other.CreateCommand();
            command.CommandText = "CREATE TABLE Photos (Id INTEGER PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        Assert.Equal("This file isn't a SpendSense backup.", (await Service.Inspect(path)).Problem);
    }

    [Fact]
    public async Task A_backup_from_a_newer_app_version_is_refused()
    {
        var path = files.PathFor("future.db");
        await using (var future = OpenPlaintext(path))
        {
            await future.Database.MigrateAsync(Ct);
            await future.Database.ExecuteSqlRawAsync(
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_FromTheFuture', '99.0');", Ct);
        }

        var info = await Service.Inspect(path);

        Assert.False(info.IsValid);
        Assert.Contains("newer version", info.Problem);
    }

    [Fact]
    public async Task Restoring_brings_back_the_backed_up_data_and_keeps_an_encrypted_safety_copy()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "Before backup");
        var backup = await Service.CreateBackup();
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1, description: "After backup");

        await Service.Restore(backup);

        Assert.Equal(["Before backup"], await Db.Transactions.Select(t => t.Description).ToListAsync(Ct));
        Assert.False(SqliteCipher.IsPlaintext(DatabasePaths.Database(files)));
        var safety = Assert.Single(Directory.GetFiles(Service.SafetyCopyDirectory));
        Assert.False(SqliteCipher.IsPlaintext(safety));
        Assert.True(SqliteCipher.Opens(safety, Key.Value));
    }

    [Fact]
    public async Task A_backup_from_another_device_restores_once_unlocked()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "From the old phone");
        var backup = await Service.CreateBackup();
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1, description: "On the new phone");
        var newPhone = OtherDevice;

        await Assert.ThrowsAsync<InvalidOperationException>(() => newPhone.Restore(backup));
        var info = await newPhone.Unlock(backup, UnlockMethod.Password, Password);
        await newPhone.Restore(backup, info);

        Assert.Equal(["From the old phone"], await Db.Transactions.Select(t => t.Description).ToListAsync(Ct));
        Assert.True(SqliteCipher.Opens(DatabasePaths.Database(files), Key.Value));
    }

    [Fact]
    public async Task A_plaintext_backup_from_before_encryption_still_restores_into_the_encrypted_database()
    {
        var path = files.PathFor("SpendSense-backup-2026-10-04-0930.db");
        await using (var old = OpenPlaintext(path))
        {
            await old.Database.MigrateAsync(Ct);
            old.Transactions.Add(Make.Transaction(TransactionTypeEnum.Expense, 42, accountId: 1, description: "Plaintext backup"));
            await old.SaveChangesAsync(Ct);
        }

        var info = await Service.Inspect(path);
        Assert.True(info.IsValid, info.Problem);
        await Service.Restore(path, info);

        Assert.Equal(["Plaintext backup"], await Db.Transactions.Select(t => t.Description).ToListAsync(Ct));
        Assert.False(SqliteCipher.IsPlaintext(DatabasePaths.Database(files)));
    }

    [Fact]
    public async Task Restoring_clears_entities_tracked_before_the_restore()
    {
        var backup = await Service.CreateBackup();
        var spare = await AddAccount("Spare");

        await Service.Restore(backup);

        Assert.Empty(Db.ChangeTracker.Entries());
        Assert.Null(await Db.Accounts.FirstOrDefaultAsync(a => a.Id == spare.Id, Ct));
    }

    [Fact]
    public async Task Only_the_newest_three_safety_copies_are_kept()
    {
        var backup = await Service.CreateBackup();

        for (var i = 0; i < 5; i++)
        {
            await Service.Restore(backup);
            await Task.Delay(1100, Ct); // safety copies are stamped to the second
        }

        Assert.Equal(3, Directory.GetFiles(Service.SafetyCopyDirectory).Length);
    }

    [Fact]
    public async Task An_older_backup_is_upgraded_when_restored()
    {
        // A backup made before accounts existed.
        var path = files.PathFor("old.db");
        await using (var old = OpenPlaintext(path))
        {
            old.GetService<IMigrator>().Migrate("20260601172005_MakeRecurringTransactionIdNullable");
            await old.Database.ExecuteSqlRawAsync("""
                INSERT INTO Transactions (Amount, CreatedAt, CurrencyId, Description, TransactionDate, TransactionType, UpdatedAt)
                VALUES (42, '2026-01-01 00:00:00', 1, 'Old transaction', '2026-01-01 00:00:00', 'Expense', '2026-01-01 00:00:00');
                """, Ct);
        }

        Assert.True((await Service.Inspect(path)).NeedsUpgrade);
        await Service.Restore(path);

        var t = await Db.Transactions.Include(x => x.Account).SingleAsync(Ct);
        Assert.Equal("Old transaction", t.Description);
        Assert.Equal("Main account", t.Account?.Name);
        Assert.Empty(await Db.Database.GetPendingMigrationsAsync(Ct));
    }

    [Fact]
    public async Task Restoring_an_invalid_file_changes_nothing()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var path = files.PathFor("bad.db");
        File.WriteAllText(path, "nope");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.Restore(path));

        Assert.Equal(1, await Db.Transactions.CountAsync(Ct));
        Assert.False(Directory.Exists(Service.SafetyCopyDirectory));
    }

    [Fact]
    public async Task Nothing_decrypted_is_left_in_the_cache_after_a_backup_and_a_restore()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "Secret groceries");
        var backup = await Service.CreateBackup();
        await Service.Restore(backup);

        foreach (var file in Directory.GetFiles(files.CacheDirectory, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(file, Ct);
            Assert.DoesNotContain("Secret groceries", Encoding.UTF8.GetString(bytes));
            Assert.False(bytes.Length >= 16 && Encoding.ASCII.GetString(bytes, 0, 16) == "SQLite format 3\0", file);
        }
        Assert.Equal([Path.GetFileName(backup)], Directory.GetFiles(files.CacheDirectory, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Csv_export_has_a_bom_a_header_and_every_transaction_oldest_first()
    {
        await AddTransaction(TransactionTypeEnum.Income, 3000, accountId: 1, description: "Salary", days: -1);
        await AddTransaction(TransactionTypeEnum.Expense, 4.2, accountId: 1, description: "Coffee");

        var path = await Service.ExportTransactionsCsv(new DateTime(2026, 10, 4));

        Assert.Equal("SpendSense-transactions-2026-10-04.csv", Path.GetFileName(path));
        var bytes = await File.ReadAllBytesAsync(path, Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = (await File.ReadAllTextAsync(path, Ct)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(TransactionCsv.Header, lines[0]);
        Assert.Contains("Salary,Income,,Main account,,3000.00,GBP,", lines[1]);
        Assert.Contains("Coffee,Expense,,Main account,,-4.20,GBP,", lines[2]);
    }

    [Fact]
    public async Task Backed_up_and_snooze_are_remembered()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var now = new DateTime(2026, 10, 4);
        Assert.True(await Service.NeedsReminder(now));

        Service.MarkBackedUp(now);
        Assert.Equal(now, Settings.LastBackupAt);
        Assert.False(await Service.NeedsReminder(now.AddDays(10)));

        Assert.True(await Service.NeedsReminder(now.AddDays(31)));
        Service.SnoozeReminder(now.AddDays(31));
        Assert.False(await Service.NeedsReminder(now.AddDays(40)));
    }

    void IDisposable.Dispose()
    {
        Dispose();
        files.Dispose();
    }
}
