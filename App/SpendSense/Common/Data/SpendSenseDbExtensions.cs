using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Security;

namespace SpendSense.Common.Data;

public static class SpendSenseDbExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The encrypted database (ADR 0004): EF over SQLite3 Multiple Ciphers, keyed on every connection by
        /// <see cref="DatabaseKeyInterceptor"/>. Nothing may query it until <see cref="DatabaseOpener.Open"/> has
        /// loaded the key; <c>DatabaseGate</c> makes the UI wait for that.
        /// </summary>
        public IServiceCollection AddSpendSenseDb()
        {
            // The .Sqlite.Core provider has no bundled engine: wire up SQLite3 Multiple Ciphers first.
            SQLitePCL.Batteries_V2.Init();

            return services
                .AddSingleton<DatabaseKey>()
                .AddSingleton<DatabaseKeyInterceptor>()
                .AddSingleton<DatabaseOpener>()
                .AddSingleton<BackupKeys>()
                .AddDbContext<SpendSenseDbContext>((provider, x) =>
                {
                    var path = DatabasePaths.Database(provider.GetRequiredService<IFileSystem>());
                    x.UseSqlite($"Data Source={path}");
                    x.AddInterceptors(new AddEntityInterceptor(), new ModifyEntityInterceptor(), provider.GetRequiredService<DatabaseKeyInterceptor>());
                })
                .AddTransient<CurrencyRepository>()
                .AddTransient<AccountRepository>()
                .AddTransient<CategoryRepository>()
                .AddTransient<TransactionRepository>()
                .AddTransient<MonthlyBudgetRepository>()
                .AddTransient<GoalRepository>()
                .AddTransient<RecurringTransactionRepository>();
        }
    }
}
