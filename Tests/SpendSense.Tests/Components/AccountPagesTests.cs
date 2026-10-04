using Bunit;

using SpendSense.Common.Models.Enums;
using SpendSense.Components.Pages.Accounts;
using SpendSense.Tests.Support;

using DetailPage = SpendSense.Components.Pages.Accounts.Detail;
using ListingPage = SpendSense.Components.Pages.Accounts.Listing;

namespace SpendSense.Tests.Components;

/// <summary>Smoke renders of the account screens over a seeded database: they load, group and add up.</summary>
public class AccountPagesTests : ComponentTest
{
    [Fact]
    public async Task Accounts_page_groups_accounts_under_an_Available_total()
    {
        var main = Db.Accounts.Single(a => a.Id == 1);
        main.OpeningBalance = 1000;
        await AddAccount("Visa card", AccountTypeEnum.CreditCard, opening: -200, limit: 1000);
        await AddAccount("Rainy day", AccountTypeEnum.Savings, opening: 500, includeInAvailable: false);

        var cut = Render<ListingPage>();

        cut.WaitForAssertion(() => Assert.Equal(["Everyday", "Savings", "Credit cards"], cut.FindAll("h2.group-title").Select(h => h.TextContent)));
        Assert.Equal("£800.00", cut.Find(".hero .amount .money").TextContent);
        Assert.Contains("£800.00 of £1,000 left", cut.Markup);
        Assert.Contains("Not counted in Available", cut.Markup);
    }

    [Fact]
    public async Task Pots_are_nested_under_their_parent()
    {
        var monzo = await AddAccount("Monzo", opening: 440);
        await AddAccount("Holiday", AccountTypeEnum.Savings, opening: 700, includeInAvailable: false, parentId: monzo.Id);

        var cut = Render<ListingPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".nested")));
        Assert.Contains("Holiday", cut.Find(".nested").TextContent);
        Assert.Contains("£1,140.00 with pots", cut.Markup);
    }

    [Fact]
    public async Task Detail_page_shows_each_transaction_with_the_balance_after_it()
    {
        var main = Db.Accounts.Single(a => a.Id == 1);
        main.OpeningBalance = 1000;
        await AddTransaction(TransactionTypeEnum.Expense, 50, main.Id, description: "Groceries", days: -1);
        await AddTransaction(TransactionTypeEnum.Income, 200, main.Id, description: "Salary");

        var cut = Render<DetailPage>(p => p.Add(x => x.Id, main.Id));

        cut.WaitForAssertion(() => Assert.Equal("Main account", cut.Find("h1").TextContent));
        Assert.Equal("£1,150.00", cut.Find(".hero .amount .money").TextContent);
        Assert.Contains("Income · £1,150.00 after", cut.Markup);
        Assert.Contains("Uncategorised · £950.00 after", cut.Markup);
    }

    [Fact]
    public void Detail_page_for_a_missing_account_says_so()
    {
        var cut = Render<DetailPage>(p => p.Add(x => x.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Account not found", cut.Markup));
    }

    [Fact]
    public async Task Balance_check_banner_lists_accounts_and_can_be_dismissed()
    {
        await AddTransaction(TransactionTypeEnum.Expense, 10, accountId: 1);
        var balances = await new SpendSense.Common.Services.AccountBalanceService(Db, Settings).GetBalances();
        var dismissed = false;

        var cut = Render<BalanceCheckBanner>(p => p
            .Add(x => x.Accounts, balances)
            .Add(x => x.OnDismiss, () => dismissed = true));

        Assert.Equal("Main account", cut.Find(".account .name").TextContent);
        cut.Find("button.dismiss").Click();
        Assert.True(dismissed);
    }
}
