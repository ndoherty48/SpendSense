using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Data;

/// <summary>Moves between accounts in different currencies: balances and the suggested rate (ADR 0004).</summary>
public class CrossCurrencyTests : DatabaseTest
{
    AccountBalanceService Service => new(Db, Settings);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    async Task<Currency> AddCurrency(string code, string symbol)
    {
        var currency = new Currency { Code = code, Name = code, Symbol = symbol };
        Db.Currencies.Add(currency);
        await Db.SaveChangesAsync(Ct);
        return currency;
    }

    async Task Move(double amount, int from, int to, double received, int days = 0)
    {
        var t = Make.Transaction(TransactionTypeEnum.Transfer, amount, from, to, date: DateTime.Today.AddDays(days), amountReceived: received);
        Db.Transactions.Add(t);
        await Db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Each_account_moves_by_its_own_side_of_the_transfer()
    {
        var euro = await AddCurrency("EUR", "€");
        var main = await MainAccount();
        var euros = await AddAccount("Euro account", currencyId: euro.Id);
        await Move(100, main.Id, euros.Id, received: 115.2);

        Assert.Equal(-100, (await Service.GetBalance(main.Id))!.Balance, precision: 2);
        Assert.Equal(115.2, (await Service.GetBalance(euros.Id))!.Balance, precision: 2);
        var all = await Service.GetBalances();
        Assert.Equal(115.2, all.Single(b => b.Account.Id == euros.Id).Balance, precision: 2);
    }

    [Fact]
    public async Task Savings_into_a_foreign_account_counts_what_arrived()
    {
        var euro = await AddCurrency("EUR", "€");
        var main = await MainAccount();
        var euros = await AddAccount("Euro savings", AccountTypeEnum.Savings, currencyId: euro.Id);
        Db.Transactions.Add(Make.Transaction(TransactionTypeEnum.Savings, 200, main.Id, euros.Id, amountReceived: 230));
        await Db.SaveChangesAsync(Ct);

        Assert.Equal(-200, (await Service.GetBalance(main.Id))!.Balance, precision: 2);
        Assert.Equal(230, (await Service.GetBalance(euros.Id))!.Balance, precision: 2);
    }

    [Fact]
    public async Task No_previous_move_means_no_suggested_rate()
    {
        var euro = await AddCurrency("EUR", "€");

        Assert.Null(await CrossCurrency.LastRate(Db, 1, euro.Id));
    }

    [Fact]
    public async Task The_suggested_rate_comes_from_the_latest_move_either_way()
    {
        var euro = await AddCurrency("EUR", "€");
        var dollar = await AddCurrency("USD", "$");
        var main = await MainAccount();
        var euros = await AddAccount("Euro account", currencyId: euro.Id);
        var dollars = await AddAccount("Dollar account", currencyId: dollar.Id);
        await Move(100, main.Id, euros.Id, received: 110, days: -10);
        await Move(115.2, euros.Id, main.Id, received: 100, days: -1); // latest, but EUR → GBP
        await Move(100, main.Id, dollars.Id, received: 130);           // another pair entirely

        Assert.Equal(1.152, await CrossCurrency.LastRate(Db, 1, euro.Id));
        Assert.Equal(0.8681, await CrossCurrency.LastRate(Db, euro.Id, 1));
    }

    [Theory]
    [InlineData(100, 1.152, 115.2)]
    [InlineData(33.33, 1.1555, 38.51)]
    [InlineData(0.01, 0.5, 0.01)]
    public void Received_is_rounded_to_pennies(double amount, double rate, double expected)
    {
        Assert.Equal(expected, CrossCurrency.Received(amount, rate));
    }

    [Theory]
    [InlineData(100, 115.2, 1.152)]
    [InlineData(115.2, 100.0, 0.8681)]
    [InlineData(0, 100.0, null)]
    [InlineData(100, null, null)]
    public void Rate_is_derived_to_four_places(double amount, double? received, double? expected)
    {
        Assert.Equal(expected, CrossCurrency.Rate(amount, received));
    }
}
