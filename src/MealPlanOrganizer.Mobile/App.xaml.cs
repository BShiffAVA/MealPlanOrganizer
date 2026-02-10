using Microsoft.Extensions.DependencyInjection;
using MealPlanOrganizer.Mobile.Services;
using Serilog;

namespace MealPlanOrganizer.Mobile;

public partial class App : Application
{
	private readonly IServiceProvider _serviceProvider;

	public App(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;

		// Set up global exception handlers
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
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
		// Start with LoginPage - it will check auth state and navigate accordingly
		var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
		return new Window(new NavigationPage(loginPage));
	}
}