using System.Globalization;

using Bunit;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

using MudBlazor;
using MudBlazor.Services;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Services;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Support;

/// <summary>
/// bUnit context with the app's services: MudBlazor (JS interop loose, no popover provider needed), settings
/// on in-memory preferences, and a migrated in-memory SQLite database for components that query.
/// </summary>
public abstract class ComponentTest : BunitContext
{
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;

    protected ComponentTest()
    {
        // Money formatting follows the current culture; pin it so assertions are stable.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.Configure<PopoverOptions>(o => o.CheckForPopoverProvider = false);

        Services.AddSingleton<IPreferences>(Preferences);
        Services.AddSingleton<INotifier>(Notifier);
        Services.AddSingleton<SettingsService>();

        connection.Open();
        Db = new SpendSenseDbContext(new DbContextOptionsBuilder<SpendSenseDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AddEntityInterceptor(), new ModifyEntityInterceptor())
            .Options);
        Db.Database.Migrate();
        Services.AddSingleton(Db);
        Services.AddTransient<AccountRepository>();
        Services.AddTransient<TransactionRepository>();
        Services.AddTransient<CategoryRepository>();
        Services.AddTransient<CurrencyRepository>();
        Services.AddTransient<AccountBalanceService>();
        Services.AddTransient<NotificationService>();
    }

    protected InMemoryPreferences Preferences { get; } = new();

    protected RecordingNotifier Notifier { get; } = new();

    protected SpendSenseDbContext Db { get; }

    protected SettingsService Settings => Services.GetRequiredService<SettingsService>();

    protected async Task<Account> AddAccount(string name, AccountTypeEnum type = AccountTypeEnum.Current, double opening = 0,
        double? limit = null, bool includeInAvailable = true, int? parentId = null)
    {
        var account = new Account
        {
            Name = name, Type = type, OpeningBalance = opening, CreditLimit = limit,
            IncludeInAvailable = includeInAvailable, CurrencyId = 1, ParentAccountId = parentId
        };
        Db.Accounts.Add(account);
        await Db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        return account;
    }

    protected async Task AddTransaction(TransactionTypeEnum type, double amount, int accountId, int? toAccountId = null, string description = "Test", int days = 0)
    {
        Db.Transactions.Add(Make.Transaction(type, amount, accountId, toAccountId, description, DateTime.Today.AddDays(days)));
        await Db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
    }

    bool disposed;

    // xUnit may dispose through DisposeAsync, which calls Dispose(false), so clean up on either path.
    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            disposed = true;
            Db.Dispose();
            connection.Dispose();
            CultureInfo.CurrentCulture = previousCulture;
        }
        base.Dispose(disposing);
    }
}
