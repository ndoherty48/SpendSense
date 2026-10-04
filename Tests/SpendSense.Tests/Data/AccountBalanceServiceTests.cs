using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Data;

/// <summary>Balances computed by the real grouped SQL queries (enum-as-text GroupBy, ToAccountId sums).</summary>
public class AccountBalanceServiceTests : DatabaseTest
{
    AccountBalanceService Service => new(Db, Settings);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Balance_is_opening_plus_movements_up_to_today()
    {
        var main = await MainAccount();
        main.OpeningBalance = 1000;
        var savings = await AddAccount("Rainy day", AccountTypeEnum.Savings, includeInAvailable: false);
        await AddTransaction(TransactionTypeEnum.Expense, 50, main.Id);
        await AddTransaction(TransactionTypeEnum.Income, 200, main.Id);
        await AddTransaction(TransactionTypeEnum.Transfer, 100, main.Id, savings.Id);
        await AddTransaction(TransactionTypeEnum.Savings, 25, main.Id, savings.Id);
        await AddTransaction(TransactionTypeEnum.Expense, 999, main.Id, days: 3); // future-dated: not yet

        var balances = await Service.GetBalances();

        Assert.Equal(1025, balances.Single(b => b.Account.Id == main.Id).Balance, precision: 2);
        Assert.Equal(125, balances.Single(b => b.Account.Id == savings.Id).Balance, precision: 2);
    }

    [Fact]
    public async Task A_card_owes_what_was_spent_on_it()
    {
        var card = await AddAccount("Visa", AccountTypeEnum.CreditCard, limit: 2000);
        await AddTransaction(TransactionTypeEnum.Expense, 80, card.Id);

        var balance = await Service.GetBalance(card.Id);

        Assert.NotNull(balance);
        Assert.Equal(-80, balance.Balance);
        Assert.Equal(80, balance.Owed);
        Assert.Equal(1920, balance.AvailableCredit);
    }

    [Fact]
    public async Task Paying_off_a_card_reduces_what_it_owes()
    {
        var main = await MainAccount();
        var card = await AddAccount("Visa", AccountTypeEnum.CreditCard, opening: -600);
        await AddTransaction(TransactionTypeEnum.Transfer, 300, main.Id, card.Id);

        Assert.Equal(300, (await Service.GetBalance(card.Id))!.Owed);
        Assert.Equal(-300, (await Service.GetBalance(main.Id))!.Balance);
    }

    [Fact]
    public async Task Archived_accounts_are_left_out_unless_asked_for()
    {
        var old = await AddAccount("Old");
        old.IsArchived = true;
        await Db.SaveChangesAsync(Ct);

        Assert.DoesNotContain(await Service.GetBalances(), b => b.Account.Id == old.Id);
        Assert.Contains(await Service.GetBalances(includeArchived: true), b => b.Account.Id == old.Id);
    }

    [Fact]
    public async Task Summary_uses_the_default_currency_and_the_credit_setting()
    {
        var main = await MainAccount();
        main.OpeningBalance = 500;
        await AddAccount("Visa", AccountTypeEnum.CreditCard, opening: -100, limit: 1000);
        var balances = await Service.GetBalances();

        Assert.Equal(400, (await Service.GetSummary(balances)).Available);

        Settings.IncludeCreditInAvailable = true;
        Assert.Equal(1300, (await Service.GetSummary(balances)).Available);
    }

    [Fact]
    public async Task Set_balance_moves_the_opening_balance_and_adds_no_transaction()
    {
        var main = await MainAccount();
        await AddTransaction(TransactionTypeEnum.Income, 3000, main.Id);
        await AddTransaction(TransactionTypeEnum.Expense, 250, main.Id);
        var before = await Db.Transactions.CountAsync(Ct);

        await Service.SetBalance(main, 1000);

        Assert.Equal(1000, (await Service.GetBalance(main.Id))!.Balance);
        Assert.Equal(1000 - 2750, main.OpeningBalance);
        Assert.Equal(before, await Db.Transactions.CountAsync(Ct));
    }

    [Fact]
    public async Task Accounts_with_history_and_a_zero_start_need_a_balance_check()
    {
        var main = await MainAccount();
        var card = await AddAccount("Visa", AccountTypeEnum.CreditCard);
        var savings = await AddAccount("Rainy day", AccountTypeEnum.Savings);
        await AddAccount("Unused");
        await AddTransaction(TransactionTypeEnum.Expense, 10, main.Id);
        await AddTransaction(TransactionTypeEnum.Expense, 10, card.Id);
        await AddTransaction(TransactionTypeEnum.Transfer, 10, main.Id, savings.Id); // savings only receives

        var names = (await Service.NeedingBalanceCheck(await Service.GetBalances())).Select(b => b.Account.Name);

        Assert.Equal(["Main account", "Visa", "Rainy day"], names);
    }

    [Fact]
    public async Task Setting_a_balance_clears_the_balance_check()
    {
        var main = await MainAccount();
        await AddTransaction(TransactionTypeEnum.Expense, 10, main.Id);

        await Service.SetBalance(main, 1234);

        Assert.Empty(await Service.NeedingBalanceCheck(await Service.GetBalances()));
    }

    [Fact]
    public async Task Pots_keep_their_own_balances()
    {
        var monzo = await AddAccount("Monzo", opening: 500);
        var holiday = await AddAccount("Holiday", AccountTypeEnum.Savings, opening: 640, includeInAvailable: false, parentId: monzo.Id);
        await AddTransaction(TransactionTypeEnum.Transfer, 60, monzo.Id, holiday.Id);

        var groups = AccountBalanceService.Group(await Service.GetBalances());
        var group = groups.Single(g => g.Account.Account.Id == monzo.Id);

        Assert.Equal(440, group.Account.Balance);
        Assert.Equal(700, Assert.Single(group.Pots).Balance);
        Assert.Equal(1140, group.Total);
    }
}
