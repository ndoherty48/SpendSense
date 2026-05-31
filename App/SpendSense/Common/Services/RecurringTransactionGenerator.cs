using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Services;

public class RecurringTransactionGenerator(SpendSenseDbContext db)
{
    public async Task GeneratePendingTransactions()
    {
        var today = DateTime.Today;
        var activeRecurring = await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Currency)
            .Where(r => r.IsActive && r.StartDate <= today && (r.EndDate == null || r.EndDate >= today))
            .ToListAsync();

        foreach (var recurring in activeRecurring)
        {
            var lastGenerated = await db.Transactions
                .Where(t => t.RecurringTransactionId == recurring.Id)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync();

            var nextDate = GetNextOccurrence(recurring, lastGenerated?.TransactionDate);

            while (nextDate <= today)
            {
                db.Transactions.Add(new Transaction
                {
                    Description = recurring.Name,
                    Amount = recurring.Amount,
                    CurrencyId = recurring.CurrencyId,
                    CategoryId = recurring.CategoryId,
                    TransactionDate = nextDate,
                    TransactionType = recurring.Category?.Type ?? TransactionTypeEnum.Expense,
                    RecurringTransactionId = recurring.Id,
                    Category = null!,
                    Currency = null!
                });

                nextDate = GetNextOccurrence(recurring, nextDate);
            }
        }

        await db.SaveChangesAsync();
    }

    static DateTime GetNextOccurrence(RecurringTransaction recurring, DateTime? lastDate)
    {
        var from = lastDate ?? recurring.StartDate.AddDays(-1);

        return recurring.Frequency switch
        {
            FrequencyEnum.Daily => from.AddDays(1),
            FrequencyEnum.Weekly => from.AddDays(7),
            FrequencyEnum.BiWeekly => from.AddDays(14),
            FrequencyEnum.Monthly => GetNextMonthlyDate(from, recurring.DayOfMonth),
            FrequencyEnum.Quarterly => from.AddMonths(3),
            FrequencyEnum.Yearly => from.AddYears(1),
            _ => from.AddMonths(1)
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
