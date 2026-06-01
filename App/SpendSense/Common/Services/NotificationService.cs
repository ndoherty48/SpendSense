using Microsoft.EntityFrameworkCore;

using Plugin.LocalNotification;

using SpendSense.Common.Data;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Services;

public class NotificationService(SpendSenseDbContext db)
{
    // Track what we've already notified using Preferences for persistence
    static bool HasNotified(string key)
    {
        return Preferences.Get(key, false);
    }

    static void MarkNotified(string key)
    {
        Preferences.Set(key, true);
    }

    public async Task CheckBudgetThresholds()
    {
        var now = DateTime.Now;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1);

        var budgets = await db.MonthlyBudgets
            .Include(mb => mb.Category)
            .Where(mb => mb.Year == now.Year && mb.Month == now.Month)
            .ToListAsync();

        foreach (var budget in budgets)
        {
            var spent = await db.Transactions
                .Where(t => t.CategoryId == budget.CategoryId
                    && t.TransactionType == TransactionTypeEnum.Expense
                    && t.TransactionDate >= startOfMonth
                    && t.TransactionDate < endOfMonth)
                .SumAsync(t => t.Amount);

            var percentage = budget.BudgetedAmount > 0 ? spent / budget.BudgetedAmount * 100 : 0;
            var key = $"budget_{budget.CategoryId}_{now.Year}_{now.Month}";

            if (percentage >= 100 && !HasNotified($"{key}_100"))
            {
                MarkNotified($"{key}_100");
                await ShowNotification(
                    $"Budget exceeded: {budget.Category?.Name}",
                    $"You've spent £{spent:F2} of your £{budget.BudgetedAmount:F2} budget.");
            }
            else if (percentage >= 80 && percentage < 100 && !HasNotified($"{key}_80"))
            {
                MarkNotified($"{key}_80");
                await ShowNotification(
                    $"Budget warning: {budget.Category?.Name}",
                    $"You've used {percentage:F0}% of your £{budget.BudgetedAmount:F2} budget.");
            }
        }
    }

    public async Task CheckGoalMilestones()
    {
        var goals = await db.Goals
            .Include(g => g.Category)
            .Where(g => g.Status == StatusEnum.Active && g.CategoryId != null)
            .ToListAsync();

        foreach (var goal in goals)
        {
            var saved = goal.CurrentAmount + await db.Transactions
                .Where(t => t.CategoryId == goal.CategoryId && t.TransactionType == TransactionTypeEnum.Savings)
                .SumAsync(t => t.Amount);

            var percentage = goal.TargetAmount > 0 ? saved / goal.TargetAmount * 100 : 0;
            var key = $"goal_{goal.Id}";

            if (percentage >= 100 && !HasNotified($"{key}_100"))
            {
                MarkNotified($"{key}_100");
                await ShowNotification(
                    $"Goal reached: {goal.Name}! 🎉",
                    $"You've saved £{saved:F2} — target of £{goal.TargetAmount:F2} achieved!");
            }
            else if (percentage >= 50 && percentage < 55 && !HasNotified($"{key}_50"))
            {
                MarkNotified($"{key}_50");
                await ShowNotification(
                    $"Goal milestone: {goal.Name}",
                    $"You're halfway there! £{saved:F2} of £{goal.TargetAmount:F2} saved.");
            }
        }
    }

    static async Task ShowNotification(string title, string description)
    {
        var request = new NotificationRequest
        {
            NotificationId = title.GetHashCode(),
            Title = title,
            Description = description
        };
        await LocalNotificationCenter.Current.Show(request);
    }
}
