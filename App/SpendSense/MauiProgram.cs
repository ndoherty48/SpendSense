using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using MudBlazor.Services;

using SpendSense.Common.Data;
using SpendSense.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace SpendSense;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices();
		builder.Services.AddSpendSenseDb();
		builder.Services.AddSingleton<SettingsService>();
		builder.Services.AddTransient<RecurringTransactionGenerator>();

		builder.AddServiceDefaults();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		app.RunDatabaseMigrations();
		app.GenerateRecurringTransactions();
		return app;
	}
}
