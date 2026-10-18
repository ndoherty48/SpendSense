using System.Globalization;

using Bunit;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

using MudBlazor;
using MudBlazor.Services;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Interceptors;
using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;
using SpendSense.Common.Security;
using SpendSense.Common.Services;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Support;

/// <summary>
/// bUnit context with the app's services: MudBlazor (JS interop loose, no popover provider needed), settings
/// on in-memory preferences, and a migrated in-memory SQLite database for components that query. Pass
/// <c>encryptedDatabase</c> for an encrypted database file keyed like the app's, which backups need (ADR 0004).
/// </summary>
public abstract class ComponentTest : BunitContext
{
    readonly SqliteConnection connection;
    readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;

    protected ComponentTest(bool encryptedDatabase = false)
    {
        // Money formatting follows the current culture; pin it so assertions are stable.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.Configure<PopoverOptions>(o => o.CheckForPopoverProvider = false);

        Services.AddSingleton<IPreferences>(Preferences);
        Services.AddSingleton<INotifier>(Notifier);
        Services.AddSingleton<SettingsService>();

        var options = new DbContextOptionsBuilder<SpendSenseDbContext>()
            .AddInterceptors(new AddEntityInterceptor(), new ModifyEntityInterceptor());
        if (encryptedDatabase)
        {
            Key.Load(System.Security.Cryptography.RandomNumberGenerator.GetBytes(DatabaseKey.Size));
            Secrets.Set(DatabaseOpener.DeviceKeyName, Key.Value).GetAwaiter().GetResult();
            var path = DatabasePaths.Database(Files);
            Directory.CreateDirectory(DatabasePaths.Folder(Files));
            options.UseSqlite($"Data Source={path}").AddInterceptors(new DatabaseKeyInterceptor(Key));
            connection = SqliteCipher.Open(path, Key.Value);
        }
        else
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            options.UseSqlite(connection);
        }
        Db = new SpendSenseDbContext(options.Options);
        Db.Database.Migrate();
        Services.AddSingleton(Db);
        Services.AddTransient<AccountRepository>();
        Services.AddTransient<TransactionRepository>();
        Services.AddTransient<CategoryRepository>();
        Services.AddTransient<CurrencyRepository>();
        Services.AddTransient<AccountBalanceService>();
        Services.AddTransient<NotificationService>();

        Services.AddSingleton<IFileSystem>(Files);
        Services.AddSingleton<IFileExchange>(FileExchange);
        Services.AddSingleton(Key);
        Services.AddSingleton<ISecretStore>(Secrets);
        Services.AddSingleton(new BackupKeys(Secrets) { PasswordIterations = TestPasswordIterations });
        Services.AddSingleton<DatabaseOpener>();
        Services.AddSingleton<IClipboard>(Clipboard);
        Services.AddTransient<BackupService>();
        Services.AddTransient<SpendSense.Components.Pages.Data.BackupFlow>();
    }

    /// <summary>Real PBKDF2, far fewer rounds than the app's 600,000, so tests stay quick.</summary>
    public const int TestPasswordIterations = 1_000;

    protected TempFileSystem Files { get; } = new();

    protected DatabaseKey Key { get; } = new();

    protected InMemorySecretStore Secrets { get; } = new();

    protected FakeClipboard Clipboard { get; } = new();

    protected FakeFileExchange FileExchange { get; } = new();

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
            Files.Dispose();
            CultureInfo.CurrentCulture = previousCulture;
        }
        base.Dispose(disposing);
    }
}
