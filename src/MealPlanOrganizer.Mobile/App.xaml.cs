using Microsoft.Extensions.DependencyInjection;
using MealPlanOrganizer.Mobile.Services;
using Serilog;

namespace MealPlanOrganizer.Mobile;

public partial class App : Application
{
	private readonly IServiceProvider _serviceProvider;

	public App(IServiceProvider serviceProvider)
	{
		// Set up global exception handlers BEFORE InitializeComponent
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

		try
		{
			InitializeComponent();
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Failed to initialize application");
			Log.CloseAndFlush();
			throw;
		}

		_serviceProvider = serviceProvider;

		Log.Information("Application initialized successfully");
	}

	private static void OnFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
	{
		// Log all first-chance exceptions for debugging - these are logged before any catch block
		// Filter out common noise exceptions
		var exType = e.Exception.GetType().Name;
		if (exType != "OperationCanceledException" && 
		    exType != "TaskCanceledException" &&
		    !e.Exception.Message.Contains("The operation was canceled"))
		{
			Log.Debug(e.Exception, "First chance exception: {ExceptionType}", exType);
		}
	}

	private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		var exception = e.ExceptionObject as Exception;
		Log.Fatal(exception, "Unhandled exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
		Log.CloseAndFlush();
	}

	private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Log.Error(e.Exception, "Unobserved task exception occurred");
		e.SetObserved(); // Prevent the process from terminating
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			// Start with LoginPage - it will check auth state and navigate accordingly
			var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
			var window = new Window(new NavigationPage(loginPage));
			
			// Subscribe to window lifecycle events for pending ratings check on resume
			window.Resumed += OnWindowResumed;
			window.Stopped += OnWindowStopped;
			
			return window;
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Failed to create main window");
			Log.CloseAndFlush();
			throw;
		}
	}

	/// <summary>
	/// Called when the app window resumes from background.
	/// </summary>
	private async void OnWindowResumed(object? sender, EventArgs e)
	{
		Log.Information("App resumed from background");
		
		try
		{
			var appStartupService = _serviceProvider.GetService<IAppStartupService>();
			if (appStartupService != null)
			{
				await appStartupService.OnAppResumedAsync();
				await appStartupService.CheckPendingRatingsAsync();
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error handling app resume");
		}
	}

	/// <summary>
	/// Called when the app window is stopped (going to background).
	/// </summary>
	private void OnWindowStopped(object? sender, EventArgs e)
	{
		Log.Information("App going to background");
		
		try
		{
			// Reset the startup check flag for next resume
			var appStartupService = _serviceProvider.GetService<IAppStartupService>();
			if (appStartupService != null)
			{
				appStartupService.HasPerformedStartupCheck = false;
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error handling app stop");
		}
	}
}