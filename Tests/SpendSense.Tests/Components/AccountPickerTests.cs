using Bunit;

using Microsoft.Extensions.DependencyInjection;

using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Components.Shared;
using SpendSense.Tests.Support;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Components;

public class AccountPickerTests : ComponentTest
{
    static readonly IReadOnlyList<AccountBalance> Accounts =
    [
        Balance(Account(1, "Everyday"), 1482.40),
        Balance(Account(2, "Visa card", AccountTypeEnum.CreditCard), -612.30),
        Balance(Account(3, "Rainy day", AccountTypeEnum.Savings), 3250)
    ];

    [Fact]
    public void Select_shows_each_account_with_its_balance_and_marks_the_selected_one()
    {
        var cut = Render<AccountSelect>(p => p.Add(x => x.Accounts, Accounts).Add(x => x.Value, 1).Add(x => x.Label, "Paid from"));

        var pills = cut.FindAll("button.pill");
        Assert.Equal(3, pills.Count);
        Assert.Equal("true", pills[0].GetAttribute("aria-pressed"));
        Assert.Contains("£1,482.40", pills[0].TextContent);
        Assert.Contains("owed", pills[1].TextContent);
        Assert.Equal("Paid from", cut.Find(".label").TextContent);
    }

    [Fact]
    public void Select_reports_the_picked_account()
    {
        int? picked = null;
        var cut = Render<AccountSelect>(p => p.Add(x => x.Accounts, Accounts).Add(x => x.ValueChanged, v => picked = v));

        cut.FindAll("button.pill")[2].Click();

        Assert.Equal(3, picked);
    }

    [Fact]
    public void Select_can_offer_none_and_show_an_error()
    {
        int? picked = 3;
        var cut = Render<AccountSelect>(p => p
            .Add(x => x.Accounts, Accounts).Add(x => x.Value, 3).Add(x => x.ValueChanged, v => picked = v)
            .Add(x => x.AllowNone, true).Add(x => x.NoneLabel, "Not tracked").Add(x => x.Error, "Pick another"));

        cut.FindAll("button.pill")[0].Click();

        Assert.Null(picked);
        Assert.Equal("Pick another", cut.Find("[role=alert]").TextContent);
    }

    static Transaction Model(TransactionTypeEnum type, int from, int? to = null) => Transaction(type, 10, from, to);

    [Fact]
    public void Transfer_shows_from_and_to_and_swaps_them()
    {
        var model = Model(TransactionTypeEnum.Transfer, from: 1, to: 2);
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, model).Add(x => x.Accounts, Accounts));

        Assert.Equal(["From", "To"], cut.FindAll(".account-select .label").Select(l => l.TextContent));
        cut.Find("button[aria-label='Swap accounts']").Click();

        Assert.Equal(2, model.AccountId);
        Assert.Equal(1, model.ToAccountId);
    }

    [Fact]
    public void Destinations_never_include_the_source()
    {
        var model = Model(TransactionTypeEnum.Transfer, from: 1);
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, model).Add(x => x.Accounts, Accounts));

        var to = cut.FindAll(".account-select")[1];
        Assert.DoesNotContain("Everyday", to.TextContent);
    }

    [Fact]
    public void Choosing_the_destination_as_source_clears_the_destination()
    {
        var model = Model(TransactionTypeEnum.Transfer, from: 1, to: 2);
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, model).Add(x => x.Accounts, Accounts));

        // Pick "Visa card" as the source: it can't also be the destination.
        cut.FindAll(".account-select")[0].QuerySelectorAll("button.pill")[1].Click();

        Assert.Equal(2, model.AccountId);
        Assert.Null(model.ToAccountId);
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Expense, "Paid from")]
    [InlineData(TransactionTypeEnum.Income, "Paid into")]
    public void Single_account_label_follows_the_type(TransactionTypeEnum type, string label)
    {
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, Model(type, from: 1)).Add(x => x.Accounts, Accounts));

        Assert.Equal(label, cut.Find(".account-select .label").TextContent);
    }

    [Fact]
    public void Savings_offers_an_optional_destination()
    {
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, Model(TransactionTypeEnum.Savings, from: 1)).Add(x => x.Accounts, Accounts));

        Assert.Equal(["From", "Into (optional)"], cut.FindAll(".account-select .label").Select(l => l.TextContent));
        Assert.Contains("Not tracked", cut.Markup);
    }

    // A GBP account and a EUR account, with a past GBP → EUR transfer at 1.152 for the suggested rate.
    async Task<IReadOnlyList<AccountBalance>> CrossCurrencyAccounts()
    {
        var euro = new Currency { Code = "EUR", Name = "Euro", Symbol = "€" };
        Db.Currencies.Add(euro);
        await Db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var euros = await AddAccount("Euro account");
        euros.CurrencyId = euro.Id;
        await Db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        await AddTransaction(TransactionTypeEnum.Transfer, 100, 1, euros.Id, days: -3);
        var past = Db.Transactions.Single();
        past.AmountReceived = 115.2;
        await Db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        return await Services.GetRequiredService<AccountBalanceService>().GetBalances();
    }

    [Fact]
    public void One_currency_shows_no_exchange_fields()
    {
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, Model(TransactionTypeEnum.Transfer, from: 1, to: 3)).Add(x => x.Accounts, Accounts));

        Assert.Empty(cut.FindAll(".exchange"));
    }

    [Fact]
    public async Task Choosing_a_foreign_destination_suggests_the_last_rate()
    {
        var accounts = await CrossCurrencyAccounts();
        var model = Transaction(TransactionTypeEnum.Transfer, 50, accountId: 1);
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, model).Add(x => x.Accounts, accounts));

        cut.FindAll(".account-select")[1].QuerySelectorAll("button.pill").Single(b => b.TextContent.Contains("Euro account")).Click();

        cut.WaitForAssertion(() => Assert.Equal(57.6, model.AmountReceived));
        Assert.Equal("1.152", cut.Find(".rate input").GetAttribute("value"));
    }

    [Fact]
    public async Task Typing_the_amount_received_sets_the_rate_and_changing_the_amount_keeps_it()
    {
        var accounts = await CrossCurrencyAccounts();
        var euros = accounts.Single(a => a.Account.Name == "Euro account").Account.Id;
        var model = Transaction(TransactionTypeEnum.Transfer, 100, accountId: 1, toAccountId: euros);
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, model).Add(x => x.Accounts, accounts));

        cut.Find(".received input").Change("120");
        Assert.Equal(120, model.AmountReceived);
        Assert.Equal("1.2", cut.Find(".rate input").GetAttribute("value"));

        model.Amount = 50;
        cut.Render(p => p.Add(x => x.Model, model));
        Assert.Equal(60, model.AmountReceived);
    }

    [Fact]
    public async Task Swapping_a_move_between_currencies_swaps_the_amounts()
    {
        var accounts = await CrossCurrencyAccounts();
        var euros = accounts.Single(a => a.Account.Name == "Euro account").Account.Id;
        var model = Transaction(TransactionTypeEnum.Transfer, 100, accountId: 1, toAccountId: euros, amountReceived: 115.2);
        var cut = Render<AccountFields>(p => p.Add(x => x.Model, model).Add(x => x.Accounts, accounts));

        cut.Find("button[aria-label='Swap accounts']").Click();

        Assert.Equal(euros, model.AccountId);
        Assert.Equal(115.2, model.Amount);
        Assert.Equal(100, model.AmountReceived);
        Assert.Equal("0.8681", cut.Find(".rate input").GetAttribute("value"));
    }
}
