using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SpendSense.Common.Data;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Services;

public class RecurringTransactionGenerator(SpendSenseDbContext db, ILogger<RecurringTransactionGenerator> logger)
{
    public async Task GeneratePendingTransactions()
    {
        var today = DateTime.Today;
        var activeRecurring = await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Currency)
            .Where(r => r.IsActive && r.StartDate <= today && (r.EndDate == null || r.EndDate >= today))
            .ToListAsync();

        logger.LogInformation("Found {Count} active recurring transactions", activeRecurring.Count);
        Console.WriteLine($"[RecurringGen] Found {activeRecurring.Count} active recurring transactions");

        foreach (var recurring in activeRecurring)
        {
            var lastGenerated = await db.Transactions
                .Where(t => t.RecurringTransactionId == recurring.Id)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync();

            var nextDate = GetNextOccurrence(recurring, lastGenerated?.TransactionDate);
            logger.LogInformation("Recurring '{Name}': last={Last}, next={Next}", recurring.Name, lastGenerated?.TransactionDate, nextDate);
            Console.WriteLine($"[RecurringGen] '{recurring.Name}': last={lastGenerated?.TransactionDate}, next={nextDate}");

            var count = 0;
            while (nextDate <= today)
            {
                db.Transactions.Add(new Transaction
                {
                    Description = recurring.Name,
                    Amount = recurring.Amount,
                    CurrencyId = recurring.CurrencyId,
                    AccountId = recurring.AccountId,
                    ToAccountId = recurring.ToAccountId,
                    CategoryId = recurring.CategoryId,
                    TransactionDate = nextDate,
                    TransactionType = recurring.TransactionType,
                    RecurringTransactionId = recurring.Id
                });

                nextDate = GetNextOccurrence(recurring, nextDate);
                count++;
            }

            logger.LogInformation("Generated {Count} transactions for '{Name}'", count, recurring.Name);
            Console.WriteLine($"[RecurringGen] Generated {count} transactions for '{recurring.Name}'");
        }

        await db.SaveChangesAsync();
    }

    static DateTime GetNextOccurrence(RecurringTransaction recurring, DateTime? lastDate)
    {
        // If no previous transaction, the start date itself is the first occurrence
        if (lastDate is null)
            return recurring.StartDate;

        return recurring.Frequency switch
        {
            FrequencyEnum.Daily => lastDate.Value.AddDays(1),
            FrequencyEnum.Weekly => lastDate.Value.AddDays(7),
            FrequencyEnum.BiWeekly => lastDate.Value.AddDays(14),
            FrequencyEnum.Monthly => GetNextMonthlyDate(lastDate.Value, recurring.DayOfMonth),
            FrequencyEnum.Quarterly => lastDate.Value.AddMonths(3),
            FrequencyEnum.Yearly => lastDate.Value.AddYears(1),
            _ => lastDate.Value.AddMonths(1)
        };
    }

    static DateTime GetNextMonthlyDate(DateTime from, int? dayOfMonth)
    {
        var nextMonth = from.AddMonths(1);
        var day = dayOfMonth ?? from.Day;
        var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        return new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(day, daysInMonth));
    }
}
