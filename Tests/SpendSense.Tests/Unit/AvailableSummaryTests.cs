using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Unit;

/// <summary>The dashboard's Available figure (ADR 0002, decision 5).</summary>
public class AvailableSummaryTests
{
    // The storyboard's accounts: Available = 1,482.40 + 326.15 + 45.00 − 612.30 = 1,241.25.
    static readonly AccountBalance[] Storyboard =
    [
        Balance(Account(1, "Everyday", isDefault: true), 1482.40),
        Balance(Account(2, "Joint"), 326.15),
        Balance(Account(3, "Cash wallet", AccountTypeEnum.Cash), 45.00),
        Balance(Account(4, "Rainy day fund", AccountTypeEnum.Savings, includeInAvailable: false), 3250.00),
        Balance(Account(5, "Holiday pot", AccountTypeEnum.Savings, includeInAvailable: false), 640.00),
        Balance(Account(6, "Visa card", AccountTypeEnum.CreditCard, limit: 2000), -612.30)
    ];

    static AvailableSummary Summarise(IReadOnlyList<AccountBalance> balances, bool includeCredit = false) =>
        AccountBalanceService.Summarise(balances, defaultCurrencyId: 1, symbol: "£", includeCredit);

    [Fact]
    public void Available_counts_included_accounts_and_subtracts_card_debt()
    {
        var summary = Summarise(Storyboard);

        Assert.Equal(1241.25, summary.Available, precision: 2);
        Assert.Equal(1853.55, summary.InAccounts, precision: 2);
        Assert.Equal(612.30, summary.OwedOnCards, precision: 2);
        Assert.Equal(3890.00, summary.Savings, precision: 2);
        Assert.Equal("£", summary.Symbol);
        Assert.Empty(summary.OtherCurrencies);
    }

    [Fact]
    public void Counting_credit_adds_what_is_left_on_cards()
    {
        Assert.Equal(1241.25 + 1387.70, Summarise(Storyboard, includeCredit: true).Available, precision: 2);
    }

    [Fact]
    public void Archived_accounts_are_ignored()
    {
        IReadOnlyList<AccountBalance> balances =
        [
            Balance(Account(1, "Everyday"), 100),
            Balance(Account(2, "Old account", archived: true), 900)
        ];

        Assert.Equal(100, Summarise(balances).Available);
    }

    [Fact]
    public void Other_currencies_are_listed_separately_and_not_converted()
    {
        IReadOnlyList<AccountBalance> balances =
        [
            Balance(Account(1, "Everyday"), 100),
            Balance(Account(2, "Euro cash", AccountTypeEnum.Cash, currency: Eur), 50),
            Balance(Account(3, "Euro card", AccountTypeEnum.CreditCard, currency: Eur), -20)
        ];

        var summary = Summarise(balances);

        Assert.Equal(100, summary.Available);
        var euro = Assert.Single(summary.OtherCurrencies);
        Assert.Equal("€", euro.Symbol);
        Assert.Equal(30, euro.Amount);
    }

    [Fact]
    public void Savings_total_includes_savings_left_out_of_Available()
    {
        IReadOnlyList<AccountBalance> balances =
        [
            Balance(Account(1, "Rainy day", AccountTypeEnum.Savings, includeInAvailable: false), 500),
            Balance(Account(2, "Counted savings", AccountTypeEnum.Savings), 200)
        ];

        var summary = Summarise(balances);

        Assert.Equal(700, summary.Savings);
        Assert.Equal(200, summary.Available);
    }

    [Fact]
    public void No_accounts_means_nothing_available()
    {
        var summary = Summarise([]);

        Assert.Equal(0, summary.Available);
        Assert.Equal(0, summary.OwedOnCards);
    }
}
