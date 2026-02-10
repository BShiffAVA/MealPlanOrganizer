using Microsoft.UI.Xaml;
using Serilog;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MealPlanOrganizer.Mobile.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
		
		// Windows-specific unhandled exception handler
		this.UnhandledException += OnUnhandledException;
	}

	private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		// Log the exception before the app crashes
		Log.Fatal(e.Exception, "Windows UnhandledException: {Message}", e.Message);
		Log.CloseAndFlush();
		
		// Allow exception to propagate (app will still crash, but we've logged it)
		e.Handled = false;
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

