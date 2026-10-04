using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using SpendSense.Common.Models.Enums;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Data;

/// <summary>
/// Migrations run on every launch against the user's real data, so the upgrade paths are tested from an
/// older schema with data in it, not just on an empty database.
/// </summary>
public class MigrationTests() : DatabaseTest(migrate: false)
{
    const string BeforeAccounts = "20260601172005_MakeRecurringTransactionIdNullable";
    const string AddAccounts = "20261004092123_AddAccounts";

    void MigrateTo(string? target = null) => Db.GetService<IMigrator>().Migrate(target);

    // A database as it was before accounts: GBP (seeded) plus EUR and an unused USD, with rows in both.
    void SeedPreAccountsData()
    {
        Sql("""
            INSERT INTO Currencies (Id, Code, Name, Symbol, ExchangeRate, IsDefault, CreatedAt, UpdatedAt) VALUES
              (2, 'EUR', 'Euro', '€', 1.17, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00'),
              (3, 'USD', 'US Dollar', '$', 1.3, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
            INSERT INTO RecurringTransactions (Id, Amount, CreatedAt, CurrencyId, Frequency, IsActive, Name, StartDate, UpdatedAt, TransactionType) VALUES
              (1, 1200, '2026-01-01 00:00:00', 1, 'Monthly', 1, 'Rent', '2026-01-01 00:00:00', '2026-01-01 00:00:00', 'Expense'),
              (2, 10, '2026-01-01 00:00:00', 2, 'Monthly', 1, 'Euro subscription', '2026-01-01 00:00:00', '2026-01-01 00:00:00', 'Expense');
            INSERT INTO Transactions (Id, Amount, CreatedAt, CurrencyId, Description, TransactionDate, TransactionType, UpdatedAt, RecurringTransactionId) VALUES
              (1, 50, '2026-01-01 00:00:00', 1, 'Groceries', '2026-09-01 00:00:00', 'Expense', '2026-01-01 00:00:00', NULL),
              (2, 3000, '2026-01-01 00:00:00', 1, 'Salary', '2026-09-01 00:00:00', 'Income', '2026-01-01 00:00:00', NULL),
              (3, 20, '2026-01-01 00:00:00', 2, 'Paris lunch', '2026-09-02 00:00:00', 'Expense', '2026-01-01 00:00:00', NULL),
              (4, 1200, '2026-01-01 00:00:00', 1, 'Rent', '2026-09-01 00:00:00', 'Expense', '2026-01-01 00:00:00', 1);
            """);
    }

    [Fact]
    public async Task Fresh_install_gets_one_default_main_account()
    {
        MigrateTo();

        var account = Assert.Single(await Db.Accounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, account.Id);
        Assert.Equal("Main account", account.Name);
        Assert.Equal(AccountTypeEnum.Current, account.Type);
        Assert.True(account.IsDefault);
        Assert.Equal(1, account.CurrencyId);
    }

    [Fact]
    public void The_model_has_no_changes_missing_a_migration()
    {
        MigrateTo();

        Assert.False(Db.Database.HasPendingModelChanges(), "The model changed without a migration: run dotnet ef migrations add.");
    }

    [Fact]
    public async Task Upgrade_assigns_existing_rows_to_an_account_in_their_currency()
    {
        MigrateTo(BeforeAccounts);
        SeedPreAccountsData();

        MigrateTo();

        var ct = TestContext.Current.CancellationToken;
        var accounts = await Db.Accounts.OrderBy(a => a.Id).ToListAsync(ct);
        Assert.Equal(["Main account", "Main account (EUR)"], accounts.Select(a => a.Name));
        Assert.Equal([1, 2], accounts.Select(a => a.CurrencyId));
        Assert.Single(accounts, a => a.IsDefault);

        // Every transaction and rule sits on the account in its own currency; unused USD gets no account.
        var eurAccount = accounts[1].Id;
        var transactions = await Db.Transactions.ToDictionaryAsync(t => t.Id, t => t.AccountId, ct);
        Assert.Equal(new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = eurAccount, [4] = 1 }, transactions);
        var rules = await Db.RecurringTransactions.ToDictionaryAsync(r => r.Id, r => r.AccountId, ct);
        Assert.Equal(new Dictionary<int, int> { [1] = 1, [2] = eurAccount }, rules);

        Assert.Empty(ForeignKeyViolations());
    }

    [Fact]
    public async Task Upgrade_works_when_the_seeded_currency_was_deleted()
    {
        MigrateTo(BeforeAccounts);
        SeedPreAccountsData();
        Sql("""
            UPDATE Currencies SET IsDefault = 1 WHERE Id = 2;
            UPDATE Transactions SET CurrencyId = 2;
            UPDATE RecurringTransactions SET CurrencyId = 2;
            DELETE FROM Currencies WHERE Id = 1;
            """);

        MigrateTo();

        var ct = TestContext.Current.CancellationToken;
        var main = Assert.Single(await Db.Accounts.ToListAsync(ct));
        Assert.Equal(2, main.CurrencyId);
        Assert.All(await Db.Transactions.ToListAsync(ct), t => Assert.Equal(main.Id, t.AccountId));
        Assert.Empty(ForeignKeyViolations());
    }

    [Fact]
    public async Task Accounts_can_be_rolled_back_and_reapplied_with_data()
    {
        MigrateTo(BeforeAccounts);
        SeedPreAccountsData();
        MigrateTo();

        MigrateTo(BeforeAccounts);
        Assert.Equal(4, await Db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Transactions").SingleAsync(TestContext.Current.CancellationToken));

        MigrateTo();
        Assert.Equal(2, await Db.Accounts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(ForeignKeyViolations());
    }

    [Fact]
    public async Task Pots_migration_applies_on_top_of_existing_accounts()
    {
        MigrateTo(AddAccounts);
        Sql("""
            INSERT INTO Accounts (Id, Name, Type, CurrencyId, OpeningBalance, IncludeInAvailable, IsDefault, IsArchived, SortOrder, CreatedAt, UpdatedAt)
              VALUES (2, 'Monzo', 'Current', 1, 100, 1, 0, 0, 0, '2026-10-01 00:00:00', '2026-10-01 00:00:00');
            INSERT INTO Transactions (Id, Amount, CreatedAt, CurrencyId, Description, TransactionDate, TransactionType, UpdatedAt, AccountId)
              VALUES (1, 50, '2026-10-01 00:00:00', 1, 'Groceries', '2026-10-01 00:00:00', 'Expense', '2026-10-01 00:00:00', 2);
            """);

        MigrateTo();

        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(["Main account", "Monzo"], await Db.Accounts.OrderBy(a => a.Id).Select(a => a.Name).ToListAsync(ct));
        Assert.All(await Db.Accounts.ToListAsync(ct), a => Assert.Null(a.ParentAccountId));
        Assert.Equal(2, (await Db.Transactions.SingleAsync(ct)).AccountId);
        Assert.Empty(ForeignKeyViolations());
    }
}
