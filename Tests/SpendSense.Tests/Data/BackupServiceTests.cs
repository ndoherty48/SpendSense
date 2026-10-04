using System.Text;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using SpendSense.Common.Data;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Tests.Support;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Data;

/// <summary>Backups are real SQLite files: written, inspected and restored over the live (in-memory) database.</summary>
public sealed class BackupServiceTests : DatabaseTest, IDisposable
{
    readonly TempFileSystem files = new();

    BackupService Service => new(Db, files, Settings);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static SpendSenseDbContext OpenFile(string path) =>
        new(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString())
            .Options);

    [Fact]
    public async Task A_backup_is_a_complete_copy_of_the_database()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 62.10, accountId: 1, description: "Groceries");
        await AddAccount("Visa", AccountTypeEnum.CreditCard);

        var path = await Service.CreateBackup(new DateTime(2026, 10, 4, 9, 30, 0));

        Assert.Equal("SpendSense-backup-2026-10-04-093000.db", Path.GetFileName(path));
        await using var copy = OpenFile(path);
        Assert.Equal("Groceries", (await copy.Transactions.SingleAsync(Ct)).Description);
        Assert.Equal(2, await copy.Accounts.CountAsync(Ct));
        Assert.Empty(await copy.Database.GetPendingMigrationsAsync(Ct));
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
    public async Task Inspecting_a_backup_summarises_it()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, days: -3);
        await AddTransaction(TransactionTypeEnum.Income, 20, accountId: 1);
        var path = await Service.CreateBackup();

        var info = Service.Inspect(path);

        Assert.True(info.IsValid, info.Problem);
        Assert.Equal(2, info.Transactions);
        Assert.Equal(1, info.Accounts);
        Assert.Equal(DateTime.Today, info.LatestTransaction?.Date);
        Assert.False(info.NeedsUpgrade);
    }

    [Fact]
    public void A_file_that_is_not_a_database_is_refused()
    {
        var path = files.PathFor("notes.db");
        File.WriteAllText(path, "definitely not sqlite");

        var info = Service.Inspect(path);

        Assert.False(info.IsValid);
        Assert.Equal("This file isn't a SpendSense backup.", info.Problem);
    }

    [Fact]
    public void Another_apps_database_is_refused()
    {
        var path = files.PathFor("other.db");
        using (var other = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            other.Open();
            using var command = other.CreateCommand();
            command.CommandText = "CREATE TABLE Photos (Id INTEGER PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        Assert.Equal("This file isn't a SpendSense backup.", Service.Inspect(path).Problem);
    }

    [Fact]
    public async Task A_backup_from_a_newer_app_version_is_refused()
    {
        var path = await Service.CreateBackup();
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_FromTheFuture', '99.0');";
            command.ExecuteNonQuery();
        }

        var info = Service.Inspect(path);

        Assert.False(info.IsValid);
        Assert.Contains("newer version", info.Problem);
    }

    [Fact]
    public async Task Restoring_brings_back_the_backed_up_data_and_keeps_a_safety_copy()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, description: "Before backup");
        var backup = await Service.CreateBackup();
        await AddTransaction(TransactionTypeEnum.Expense, 99, accountId: 1, description: "After backup");

        await Service.Restore(backup);

        Assert.Equal(["Before backup"], await Db.Transactions.Select(t => t.Description).ToListAsync(Ct));
        var safety = Assert.Single(Directory.GetFiles(Service.SafetyCopyDirectory));
        await using var saved = OpenFile(safety);
        Assert.Equal(2, await saved.Transactions.CountAsync(Ct));
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
        await using (var old = OpenFile(path))
        {
            old.GetService<IMigrator>().Migrate("20260601172005_MakeRecurringTransactionIdNullable");
            await old.Database.ExecuteSqlRawAsync("""
                INSERT INTO Transactions (Amount, CreatedAt, CurrencyId, Description, TransactionDate, TransactionType, UpdatedAt)
                VALUES (42, '2026-01-01 00:00:00', 1, 'Old transaction', '2026-01-01 00:00:00', 'Expense', '2026-01-01 00:00:00');
                """, Ct);
        }

        Assert.True(Service.Inspect(path).NeedsUpgrade);
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
