using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Unit;

public class AccountBalanceTests
{
    [Theory]
    [InlineData(TransactionTypeEnum.Income, 100)]
    [InlineData(TransactionTypeEnum.Expense, -100)]
    [InlineData(TransactionTypeEnum.Savings, -100)]
    [InlineData(TransactionTypeEnum.Transfer, -100)]
    public void Delta_on_the_source_account(TransactionTypeEnum type, double expected)
    {
        var t = Transaction(type, 100, accountId: 1, toAccountId: type is TransactionTypeEnum.Transfer or TransactionTypeEnum.Savings ? 2 : null);

        Assert.Equal(expected, AccountBalanceService.Delta(t, accountId: 1));
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Transfer)]
    [InlineData(TransactionTypeEnum.Savings)]
    public void Delta_on_the_destination_account_is_positive(TransactionTypeEnum type)
    {
        var t = Transaction(type, 100, accountId: 1, toAccountId: 2);

        Assert.Equal(100, AccountBalanceService.Delta(t, accountId: 2));
    }

    [Fact]
    public void Delta_on_an_unrelated_account_is_zero()
    {
        var t = Transaction(TransactionTypeEnum.Transfer, 100, accountId: 1, toAccountId: 2);

        Assert.Equal(0, AccountBalanceService.Delta(t, accountId: 3));
    }

    [Fact]
    public void A_card_in_debt_owes_the_negated_balance()
    {
        var card = Balance(Account(1, "Visa", AccountTypeEnum.CreditCard, limit: 2000), -612.30);

        Assert.True(card.IsCard);
        Assert.Equal(612.30, card.Owed, precision: 2);
        Assert.Equal(1387.70, card.AvailableCredit!.Value, precision: 2);
        Assert.Equal(612.30 / 2000 * 100, card.CreditUsedPercent!.Value, precision: 6);
    }

    [Fact]
    public void A_card_in_credit_owes_nothing_and_has_the_full_limit()
    {
        var card = Balance(Account(1, "Visa", AccountTypeEnum.CreditCard, limit: 2000), 25);

        Assert.Equal(0, card.Owed);
        Assert.Equal(2000, card.AvailableCredit);
    }

    [Fact]
    public void A_card_over_its_limit_has_no_credit_left()
    {
        var card = Balance(Account(1, "Visa", AccountTypeEnum.CreditCard, limit: 500), -650);

        Assert.Equal(0, card.AvailableCredit);
        Assert.True(card.CreditUsedPercent > 100);
    }

    [Fact]
    public void A_card_without_a_limit_has_no_credit_figures()
    {
        var card = Balance(Account(1, "Visa", AccountTypeEnum.CreditCard), -80);

        Assert.Equal(80, card.Owed);
        Assert.Null(card.AvailableCredit);
        Assert.Null(card.CreditUsedPercent);
    }

    [Fact]
    public void Non_card_accounts_never_owe()
    {
        var overdrawn = Balance(Account(1, "Everyday"), -50);

        Assert.False(overdrawn.IsCard);
        Assert.Equal(0, overdrawn.Owed);
        Assert.Null(overdrawn.AvailableCredit);
    }

    [Fact]
    public void Symbol_follows_the_account_currency()
    {
        Assert.Equal("€", Balance(Account(1, "Euro cash", AccountTypeEnum.Cash, currency: Eur), 10).Symbol);
    }
}
