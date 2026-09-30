using SpendSense.Common.Services;

namespace SpendSense;

public partial class App : Application
{
	public App(ThemeService themeService)
	{
		InitializeComponent();

		// Apply the saved theme mode to the native app before the first window shows,
		// so native chrome never flashes the wrong theme.
		themeService.ApplyNativeTheme();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "SpendSense" };
	}
}
