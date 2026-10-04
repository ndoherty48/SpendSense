using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Tests.Support;

namespace SpendSense.Tests.Data;

/// <summary>The generator runs on every launch and catches up on everything due.</summary>
public class RecurringTransactionGeneratorTests : DatabaseTest
{
    RecurringTransactionGenerator Generator => new(Db, NullLogger<RecurringTransactionGenerator>.Instance);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static readonly DateTime Today = DateTime.Today;

    async Task<RecurringTransaction> AddRule(FrequencyEnum frequency, DateTime start, TransactionTypeEnum type = TransactionTypeEnum.Expense,
        int? dayOfMonth = null, DateTime? end = null, bool active = true, int? toAccountId = null)
    {
        var rule = new RecurringTransaction
        {
            Name = "Rule", Amount = 10, CurrencyId = 1, AccountId = 1, ToAccountId = toAccountId,
            Frequency = frequency, TransactionType = type, StartDate = start, EndDate = end,
            DayOfMonth = dayOfMonth, IsActive = active
        };
        Db.RecurringTransactions.Add(rule);
        await Db.SaveChangesAsync(Ct);
        return rule;
    }

    async Task<List<DateTime>> GeneratedDates(int ruleId) =>
        await Db.Transactions.Where(t => t.RecurringTransactionId == ruleId).OrderBy(t => t.TransactionDate)
            .Select(t => t.TransactionDate).ToListAsync(Ct);

    [Fact]
    public async Task Catches_up_every_occurrence_due_including_today()
    {
        var rule = await AddRule(FrequencyEnum.Weekly, Today.AddDays(-14));

        await Generator.GeneratePendingTransactions();

        Assert.Equal([Today.AddDays(-14), Today.AddDays(-7), Today], await GeneratedDates(rule.Id));
    }

    [Fact]
    public async Task Running_again_does_not_duplicate()
    {
        var rule = await AddRule(FrequencyEnum.Monthly, Today.AddMonths(-2));

        await Generator.GeneratePendingTransactions();
        await Generator.GeneratePendingTransactions();

        Assert.Equal(3, (await GeneratedDates(rule.Id)).Count);
    }

    [Fact]
    public async Task Inactive_future_and_ended_rules_generate_nothing()
    {
        await AddRule(FrequencyEnum.Daily, Today.AddDays(-3), active: false);
        await AddRule(FrequencyEnum.Daily, Today.AddDays(1));
        await AddRule(FrequencyEnum.Daily, Today.AddDays(-10), end: Today.AddDays(-1));

        await Generator.GeneratePendingTransactions();

        Assert.Equal(0, await Db.Transactions.CountAsync(Ct));
    }

    [Fact]
    public async Task Monthly_on_the_31st_clamps_to_short_months_and_recovers()
    {
        var start = new DateTime(Today.Year - 1, 1, 31);
        var rule = await AddRule(FrequencyEnum.Monthly, start, dayOfMonth: 31);

        await Generator.GeneratePendingTransactions();

        var dates = await GeneratedDates(rule.Id);
        Assert.Contains(new DateTime(start.Year, 2, DateTime.DaysInMonth(start.Year, 2)), dates);
        Assert.Contains(new DateTime(start.Year, 3, 31), dates);
        Assert.Contains(new DateTime(start.Year, 4, 30), dates);
    }

    [Fact]
    public async Task Generated_transactions_copy_the_rule()
    {
        var savings = await AddAccount("Rainy day", AccountTypeEnum.Savings);
        var rule = await AddRule(FrequencyEnum.Monthly, Today, TransactionTypeEnum.Transfer, toAccountId: savings.Id);

        await Generator.GeneratePendingTransactions();

        var t = await Db.Transactions.SingleAsync(x => x.RecurringTransactionId == rule.Id, Ct);
        Assert.Equal(TransactionTypeEnum.Transfer, t.TransactionType);
        Assert.Equal(1, t.AccountId);
        Assert.Equal(savings.Id, t.ToAccountId);
        Assert.Equal(10, t.Amount);
        Assert.Equal("Rule", t.Description);
    }
}
