using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Components.Shared;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Unit;

/// <summary>The account rules shared by the transaction and recurring forms.</summary>
public class AccountFieldsTests
{
    static readonly IReadOnlyList<AccountBalance> Accounts =
    [
        Balance(Account(1, "Everyday"), 1000),
        Balance(Account(2, "Visa card", AccountTypeEnum.CreditCard), -100),
        Balance(Account(3, "Rainy day", AccountTypeEnum.Savings), 500),
        Balance(Account(4, "Euro cash", AccountTypeEnum.Cash, currency: Eur), 50)
    ];

    static string? Validate(TransactionTypeEnum type, int from, int? to, double? received = null) =>
        AccountFields.Validate(Transaction(type, 10, from, to, amountReceived: received), Accounts);

    [Theory]
    [InlineData(TransactionTypeEnum.Expense)]
    [InlineData(TransactionTypeEnum.Income)]
    [InlineData(TransactionTypeEnum.Savings)]
    public void Valid_with_just_a_source(TransactionTypeEnum type)
    {
        Assert.Null(Validate(type, from: 1, to: null));
    }

    [Fact]
    public void A_source_account_is_required()
    {
        Assert.Equal("Choose an account.", Validate(TransactionTypeEnum.Expense, from: 0, to: null));
        Assert.Equal("Choose an account.", Validate(TransactionTypeEnum.Expense, from: 99, to: null));
    }

    [Fact]
    public void A_transfer_needs_a_destination()
    {
        Assert.Equal("Choose the account to move money into.", Validate(TransactionTypeEnum.Transfer, from: 1, to: null));
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Transfer)]
    [InlineData(TransactionTypeEnum.Savings)]
    public void Destination_must_differ_from_the_source(TransactionTypeEnum type)
    {
        Assert.Equal("Choose a different account to move money into.", Validate(type, from: 1, to: 1));
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Transfer)]
    [InlineData(TransactionTypeEnum.Savings)]
    public void A_move_between_currencies_needs_the_amount_received(TransactionTypeEnum type)
    {
        Assert.Equal("Enter the amount received or the rate.", Validate(type, from: 1, to: 4));
        Assert.Equal("Enter the amount received or the rate.", Validate(type, from: 1, to: 4, received: 0));
        Assert.Null(Validate(type, from: 1, to: 4, received: 11.52));
    }

    [Fact]
    public void A_move_in_one_currency_needs_no_amount_received()
    {
        Assert.Null(Validate(TransactionTypeEnum.Transfer, from: 1, to: 3));
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Transfer, 1, 4, true)]
    [InlineData(TransactionTypeEnum.Savings, 4, 1, true)]
    [InlineData(TransactionTypeEnum.Transfer, 1, 3, false)]
    [InlineData(TransactionTypeEnum.Expense, 1, 4, false)]
    public void Crossing_is_only_a_move_between_currencies(TransactionTypeEnum type, int from, int to, bool expected)
    {
        Assert.Equal(expected, AccountFields.Crossing(Transaction(type, 10, from, to), Accounts) is not null);
    }

    [Fact]
    public void Paying_off_a_card_is_valid()
    {
        Assert.Null(Validate(TransactionTypeEnum.Transfer, from: 1, to: 2));
    }

    [Fact]
    public void Destinations_are_every_other_account_in_any_currency()
    {
        var model = Transaction(TransactionTypeEnum.Transfer, 10, accountId: 1);

        var names = AccountFields.DestinationsFor(model, Accounts).Select(a => a.Account.Name);

        Assert.Equal(["Visa card", "Rainy day", "Euro cash"], names);
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Expense, null)]
    [InlineData(TransactionTypeEnum.Income, null)]
    [InlineData(TransactionTypeEnum.Savings, 3)]
    [InlineData(TransactionTypeEnum.Transfer, 3)]
    public void Normalise_keeps_a_destination_only_for_moves(TransactionTypeEnum type, int? expected)
    {
        var model = Transaction(type, 10, accountId: 1, toAccountId: 3);

        AccountFields.Normalise(model, Accounts);

        Assert.Equal(expected, model.ToAccountId);
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Transfer, 4, 11.52)]
    [InlineData(TransactionTypeEnum.Transfer, 3, null)]
    [InlineData(TransactionTypeEnum.Expense, 4, null)]
    public void Normalise_keeps_an_amount_received_only_between_currencies(TransactionTypeEnum type, int to, double? expected)
    {
        var model = Transaction(type, 10, accountId: 1, toAccountId: to, amountReceived: 11.52);

        AccountFields.Normalise(model, Accounts);

        Assert.Equal(expected, model.AmountReceived);
    }
}
