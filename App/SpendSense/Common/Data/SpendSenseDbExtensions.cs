using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Services;

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
                .AddTransient<TransactionRepository>()
                .AddTransient<MonthlyBudgetRepository>()
                .AddTransient<GoalRepository>()
                .AddTransient<RecurringTransactionRepository>();
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

        public MauiApp GenerateRecurringTransactions()
        {
            try
            {
                Console.WriteLine("[RecurringGen] Starting generation...");
                using var scope = app.Services.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<RecurringTransactionGenerator>();
                generator.GeneratePendingTransactions().GetAwaiter().GetResult();
                Console.WriteLine("[RecurringGen] Complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RecurringGen] FAILED: {ex}");
            }
            return app;
        }
    }
}