using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using SpendSense.Common.Data;

// The plain net11.0 build (dotnet ef, the tests) is an Exe with no entry point of its own, so EF needs this one.
// iOS, Mac Catalyst and Windows bring their own Main (Platforms/*/Program.cs, WinUI's generated one): a second
// would fail with CS0017.
#if !(IOS || MACCATALYST || WINDOWS)
class Program
{
    static void Main(string[] args)
    {
        // nothing needed; EF just needs this to exist
    }
}
#endif

public class MyDbContextFactory : IDesignTimeDbContextFactory<SpendSenseDbContext>
{
    public SpendSenseDbContext CreateDbContext(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();
        var optionsBuilder = new DbContextOptionsBuilder<SpendSenseDbContext>();

        optionsBuilder.UseSqlite("Data Source=my.db");

        return new SpendSenseDbContext(optionsBuilder.Options);
    }
}