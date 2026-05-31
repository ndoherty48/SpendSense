using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Data.Repositories;

namespace SpendSense.Common.Data;

public static class SpendSenseDbExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSpendSenseDb()
        {
            return services
                .AddDbContext<SpendSenseDbContext>(x =>
                {
                    var folder = Path.Join(FileSystem.AppDataDirectory, "SpendSense");
                    if(Directory.Exists(folder) is false)
                        Directory.CreateDirectory(folder);
                    var path = Path.Join(folder, "SpendSense.db");
                    x.UseSqlite($"Data Source={path}");
                    x.AddInterceptors(new AddEntityInterceptor(), new ModifyEntityInterceptor());
                })
                .AddTransient<CurrencyRepository>()
                .AddTransient<CategoryRepository>()
                .AddTransient<TransactionRepository>();
        }
    }

    extension(MauiApp app)
    {
        public MauiApp RunDatabaseMigrations()
        {
            using var scope = app.Services.CreateScope();
            var dbContext = app.Services.GetRequiredService<SpendSenseDbContext>();
            if (dbContext.Database.GetPendingMigrations().Any())
            {
                dbContext.Database.Migrate();
            }
            return app;
        }
    }
}