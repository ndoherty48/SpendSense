using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Support;

/// <summary>
/// A fresh, fully migrated SQLite database per test, in memory. Real SQLite rather than EF's InMemory provider:
/// enum-as-text columns, GroupBy translation and foreign keys behave as they do on the device.
/// xUnit creates a new instance per test, so tests never share data.
/// </summary>
public abstract class DatabaseTest : IDisposable
{
    readonly SqliteConnection connection;

    protected DatabaseTest(bool migrate = true)
    {
        // The in-memory database lives as long as this connection stays open.
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Db = CreateContext();
        if (migrate)
            Db.Database.Migrate();
    }

    protected SpendSenseDbContext Db { get; }

    protected InMemoryPreferences Preferences { get; } = new();

    protected SettingsService Settings => new(Preferences);

    /// <summary>A second context on the same database, e.g. to read back without the first one's tracked entities.</summary>
    protected SpendSenseDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AddEntityInterceptor(), new ModifyEntityInterceptor())
            .Options);

    /// <summary>Runs SQL directly, for seeding a database at an older migration.</summary>
    protected void Sql(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    protected List<string> ForeignKeyViolations()
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
            rows.Add($"{reader.GetString(0)} row {reader.GetValue(1)} -> {reader.GetString(2)}");
        return rows;
    }

    /// <summary>The seeded default account (Id 1).</summary>
    protected Task<Account> MainAccount() => Db.Accounts.SingleAsync(a => a.Id == 1);

    protected async Task<Account> AddAccount(string name, AccountTypeEnum type = AccountTypeEnum.Current, double opening = 0,
        double? limit = null, bool includeInAvailable = true, int currencyId = 1, int? parentId = null)
    {
        var account = new Account
        {
            Name = name, Type = type, OpeningBalance = opening, CreditLimit = limit,
            IncludeInAvailable = includeInAvailable, CurrencyId = currencyId, ParentAccountId = parentId
        };
        Db.Accounts.Add(account);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account;
    }

    protected async Task<Transaction> AddTransaction(TransactionTypeEnum type, double amount, int accountId, int? toAccountId = null,
        int days = 0, int? categoryId = null, string description = "Test")
    {
        var transaction = Make.Transaction(type, amount, accountId, toAccountId, description, DateTime.Today.AddDays(days), categoryId: categoryId);
        Db.Transactions.Add(transaction);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction;
    }

    protected async Task<Category> AddCategory(string name, TransactionTypeEnum type = TransactionTypeEnum.Expense)
    {
        var category = new Category { Name = name, Type = type };
        Db.Categories.Add(category);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return category;
    }

    public void Dispose()
    {
        Db.Dispose();
        connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
