using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data.Repositories;

public sealed class CategoryRepository(SpendSenseDbContext dbContext)
{
    public async Task<IReadOnlyCollection<Category>> GetAll()
    {
        return await dbContext.Categories.ToListAsync();
    }

    public async Task<Category?> GetById(int id)
    {
        return await dbContext.Categories.FindAsync(id);
    }

    public async Task<int> Add(Category category)
    {
        dbContext.Categories.Add(category);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Update(Category category)
    {
        dbContext.Categories.Update(category);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Delete(Category category)
    {
        dbContext.Categories.Remove(category);
        return await dbContext.SaveChangesAsync();
    }
}
