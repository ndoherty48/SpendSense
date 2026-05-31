using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data.Repositories;

public sealed class GoalRepository(SpendSenseDbContext dbContext)
{
    public async Task<IReadOnlyCollection<Goal>> GetAll()
    {
        return await dbContext.Goals
            .Include(g => g.Category)
            .Include(g => g.Currency)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<Goal?> GetById(int id)
    {
        return await dbContext.Goals
            .Include(g => g.Category)
            .Include(g => g.Currency)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<int> Add(Goal goal)
    {
        dbContext.Goals.Add(goal);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Update(Goal goal)
    {
        dbContext.Goals.Update(goal);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Delete(Goal goal)
    {
        dbContext.Goals.Remove(goal);
        return await dbContext.SaveChangesAsync();
    }
}
