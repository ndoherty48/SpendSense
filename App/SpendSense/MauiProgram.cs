using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using MudBlazor.Services;

using SpendSense.Common.Data;
using SpendSense.Common.Services;
using Microsoft.EntityFrameworkCore;

using Plugin.LocalNotification;

namespace SpendSense;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseLocalNotification();

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices();
		builder.Services.AddSpendSenseDb();
		builder.Services.AddSingleton(Preferences.Default);
		builder.Services.AddSingleton<INotifier, LocalNotifier>();
		builder.Services.AddSingleton(FileSystem.Current);
		builder.Services.AddSingleton(Share.Default);
		builder.Services.AddSingleton(FilePicker.Default);
		builder.Services.AddSingleton<IFileExchange, MauiFileExchange>();
		builder.Services.AddSingleton<SettingsService>();
		builder.Services.AddSingleton<ThemeService>();
		builder.Services.AddTransient<RecurringTransactionGenerator>();
		builder.Services.AddTransient<NotificationService>();
		builder.Services.AddTransient<AccountBalanceService>();
		builder.Services.AddTransient<BackupService>();
		builder.Services.AddTransient<SpendSense.Components.Pages.Data.BackupFlow>();

		builder.AddServiceDefaults();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		app.RunDatabaseMigrations();
		app.GenerateRecurringTransactions();
		app.CheckNotifications();
		return app;
	}
}
