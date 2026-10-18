using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using MudBlazor.Services;

using SpendSense.Common.Data;
using SpendSense.Common.Security;
using SpendSense.Common.Services;

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
		builder.Services.AddSingleton(Clipboard.Default);
#if WINDOWS
		// Unpackaged, so SecureStorage has no package identity to use (ADR 0004).
		builder.Services.AddSingleton<ISecretStore, SpendSense.Platforms.Windows.DpapiSecretStore>();
#elif MACCATALYST && DEBUG
		// Unsigned Debug builds can't use the Keychain; fall back to files so `dotnet run` keeps working.
		builder.Services.AddSingleton<ISecretStore>(provider => new DevFallbackSecretStore(
			new SecureStorageSecretStore(SecureStorage.Default),
			Path.Join(DatabasePaths.Folder(provider.GetRequiredService<IFileSystem>()), "dev-secrets")));
#else
		builder.Services.AddSingleton<ISecretStore>(new SecureStorageSecretStore(SecureStorage.Default));
#endif
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

		// The database is opened, migrated and brought up to date by DatabaseGate, once the device key is loaded.
		return builder.Build();
	}
}
