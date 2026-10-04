using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Data;

/// <summary>The delete guards and defaults that protect user data.</summary>
public class RepositoryTests : DatabaseTest
{
    AccountRepository Accounts => new(Db);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Adding_a_default_account_clears_the_previous_default()
    {
        await Accounts.Add(new Account { Name = "Monzo", Type = AccountTypeEnum.Current, CurrencyId = 1, IsDefault = true });

        var defaults = await Db.Accounts.Where(a => a.IsDefault).Select(a => a.Name).ToListAsync(Ct);
        Assert.Equal(["Monzo"], defaults);
    }

    [Fact]
    public async Task Default_falls_back_to_the_first_open_account()
    {
        var main = await MainAccount();
        main.IsDefault = false;
        await Db.SaveChangesAsync(Ct);

        Assert.Equal(main.Id, (await Accounts.GetDefault())?.Id);
    }

    [Theory]
    [InlineData("Main account", true)]
    [InlineData("  main ACCOUNT ", true)]
    [InlineData("Monzo", false)]
    public async Task Account_names_are_unique_ignoring_case(string name, bool exists)
    {
        Assert.Equal(exists, await Accounts.NameExists(name));
    }

    [Fact]
    public async Task An_account_does_not_clash_with_its_own_name()
    {
        Assert.False(await Accounts.NameExists("Main account", exceptId: 1));
    }

    [Fact]
    public async Task An_account_in_use_has_dependencies_and_cannot_be_deleted()
    {
        var main = await MainAccount();
        await AddTransaction(TransactionTypeEnum.Expense, 10, main.Id);
        Assert.True(await Accounts.HasDependencies(main.Id));

        // A fresh context, as on a device: the database's Restrict foreign key is what refuses the delete.
        await using var fresh = CreateContext();
        var account = await fresh.Accounts.SingleAsync(a => a.Id == main.Id, Ct);
        await Assert.ThrowsAsync<DbUpdateException>(() => new AccountRepository(fresh).Delete(account));
    }

    [Fact]
    public async Task Receiving_a_transfer_or_having_pots_counts_as_in_use()
    {
        var main = await MainAccount();
        var savings = await AddAccount("Savings", AccountTypeEnum.Savings);
        var monzo = await AddAccount("Monzo");
        await AddAccount("Holiday", AccountTypeEnum.Savings, parentId: monzo.Id);
        await AddTransaction(TransactionTypeEnum.Transfer, 10, main.Id, savings.Id);

        Assert.True(await Accounts.HasDependencies(savings.Id));
        Assert.True(await Accounts.HasDependencies(monzo.Id));
        Assert.True(await Accounts.HasPots(monzo.Id));
        Assert.True(await Accounts.HasOpenPots(monzo.Id));
    }

    [Fact]
    public async Task An_unused_account_can_be_deleted()
    {
        var spare = await AddAccount("Spare");

        Assert.False(await Accounts.HasDependencies(spare.Id));
        await Accounts.Delete(spare);
        Assert.Null(await Accounts.GetById(spare.Id));
    }

    [Fact]
    public async Task Pot_parents_are_open_top_level_non_card_accounts()
    {
        var monzo = await AddAccount("Monzo");
        await AddAccount("Holiday", AccountTypeEnum.Savings, parentId: monzo.Id);
        await AddAccount("Visa", AccountTypeEnum.CreditCard);
        var old = await AddAccount("Old");
        old.IsArchived = true;
        await Db.SaveChangesAsync(Ct);

        var names = (await Accounts.GetParentOptions(exceptId: monzo.Id)).Select(a => a.Name);

        Assert.Equal(["Main account"], names);
    }

    [Fact]
    public async Task A_currency_used_by_an_account_is_in_use()
    {
        var currencies = new CurrencyRepository(Db);

        Assert.True(await currencies.HasDependencies(1));
    }

    [Fact]
    public async Task A_category_used_by_a_transaction_is_in_use()
    {
        var categories = new CategoryRepository(Db);
        var groceries = await AddCategory("Groceries");
        Assert.False(await categories.HasDependencies(groceries.Id));

        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1, categoryId: groceries.Id);

        Assert.True(await categories.HasDependencies(groceries.Id));
    }

    [Fact]
    public async Task Timestamps_are_set_on_add_and_update()
    {
        var spare = await AddAccount("Spare");
        Assert.NotEqual(default, spare.CreatedAt);
        var created = spare.UpdatedAt;

        await Task.Delay(10, Ct);
        spare.Name = "Renamed";
        await Db.SaveChangesAsync(Ct);

        Assert.True(spare.UpdatedAt > created);
    }
}
