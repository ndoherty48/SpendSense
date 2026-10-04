using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Components.Shared;

using static SpendSense.Tests.Support.Make;

namespace SpendSense.Tests.Unit;

/// <summary>How a row reads in Activity, Recent and Recurring: sign, tone and subtitle.</summary>
public class TransactionVisualsTests
{
    static readonly Account Everyday = Account(1, "Everyday");
    static readonly Account Visa = Account(2, "Visa card", AccountTypeEnum.CreditCard);
    static readonly Account Rainy = Account(3, "Rainy day", AccountTypeEnum.Savings);

    static Transaction Row(TransactionTypeEnum type, Account from, Account? to = null, string? category = null)
    {
        var t = Transaction(type, 50, from.Id, to?.Id);
        t.Account = from;
        t.ToAccount = to;
        t.Category = category is null ? null : new Category { Name = category, Type = type };
        return t;
    }

    [Fact]
    public void Expense_is_negative_with_category_and_account()
    {
        var t = Row(TransactionTypeEnum.Expense, Everyday, category: "Groceries");

        Assert.Equal(-50, TransactionVisuals.Amount(t));
        Assert.True(TransactionVisuals.Signed(t));
        Assert.Equal(MoneyTone.Neutral, TransactionVisuals.AmountTone(t));
        Assert.Equal("Groceries · Everyday", TransactionVisuals.Subtitle(t));
    }

    [Fact]
    public void Income_is_positive_in_the_income_tone()
    {
        var t = Row(TransactionTypeEnum.Income, Everyday);

        Assert.Equal(50, TransactionVisuals.Amount(t));
        Assert.Equal(MoneyTone.Income, TransactionVisuals.AmountTone(t));
        Assert.Equal("Income · Everyday", TransactionVisuals.Subtitle(t));
    }

    [Fact]
    public void Transfer_is_a_move_shown_without_a_sign()
    {
        var t = Row(TransactionTypeEnum.Transfer, Everyday, Visa);

        Assert.True(TransactionVisuals.IsMove(t));
        Assert.False(TransactionVisuals.Signed(t));
        Assert.Equal(50, TransactionVisuals.Amount(t));
        Assert.Equal(MoneyTone.Muted, TransactionVisuals.AmountTone(t));
        Assert.Equal("Everyday → Visa card", TransactionVisuals.Subtitle(t));
    }

    [Fact]
    public void Savings_into_an_account_is_a_move_labelled_savings()
    {
        var t = Row(TransactionTypeEnum.Savings, Everyday, Rainy);

        Assert.True(TransactionVisuals.IsMove(t));
        Assert.Equal("Savings · Everyday → Rainy day", TransactionVisuals.Subtitle(t));
    }

    [Fact]
    public void Savings_without_a_destination_still_reads_as_money_out()
    {
        var t = Row(TransactionTypeEnum.Savings, Everyday);

        Assert.False(TransactionVisuals.IsMove(t));
        Assert.Equal(-50, TransactionVisuals.Amount(t));
        Assert.True(TransactionVisuals.Signed(t));
    }

    [Fact]
    public void Missing_category_and_account_fall_back_to_uncategorised()
    {
        var t = Transaction(TransactionTypeEnum.Expense, 50, accountId: 1);

        Assert.Equal("Uncategorised", TransactionVisuals.Subtitle(t));
    }

    [Fact]
    public void Recurring_rules_use_the_same_rules()
    {
        var rule = new RecurringTransaction
        {
            Name = "Standing order", Amount = 200, Frequency = FrequencyEnum.Monthly, StartDate = DateTime.Today,
            TransactionType = TransactionTypeEnum.Savings, AccountId = 1, ToAccountId = 3, Account = Everyday, ToAccount = Rainy
        };

        Assert.False(TransactionVisuals.Signed(rule));
        Assert.Equal("Savings · Everyday → Rainy day", TransactionVisuals.Subtitle(rule));
    }

    [Theory]
    [InlineData(TransactionTypeEnum.Income, IconTone.Accent)]
    [InlineData(TransactionTypeEnum.Expense, IconTone.Neutral)]
    [InlineData(TransactionTypeEnum.Transfer, IconTone.Neutral)]
    public void Icon_tone(TransactionTypeEnum type, IconTone tone)
    {
        Assert.Equal(tone, TransactionVisuals.Tone(type));
    }
}
