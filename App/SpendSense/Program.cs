using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using SpendSense.Common.Data;

class Program
{
    static void Main(string[] args)
    {
        // nothing needed; EF just needs this to exist
    }
}

public class MyDbContextFactory : IDesignTimeDbContextFactory<SpendSenseDbContext>
{
    public SpendSenseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SpendSenseDbContext>();

        optionsBuilder.UseSqlite("Data Source=my.db");

        return new SpendSenseDbContext(optionsBuilder.Options);
    }
}