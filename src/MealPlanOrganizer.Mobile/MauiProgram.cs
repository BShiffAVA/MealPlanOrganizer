using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Diagnostics;
using MealPlanOrganizer.Mobile.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MealPlanOrganizer.Mobile;

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
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Load configuration from appsettings files
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream("MealPlanOrganizer.Mobile.appsettings.json");
		
		var config = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
			.Build();
		
		builder.Configuration.AddConfiguration(config);

		// Register services
		builder.Services.AddHttpClient<RecipeService>();
		builder.Services.AddSingleton<IRecipeService>(sp => sp.GetRequiredService<RecipeService>());

		// Register authentication service
		builder.Services.AddSingleton<IAuthService, AuthService>();

		// Register pages for dependency injection
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<ExtractRecipePage>();
		builder.Services.AddTransient<ExtractedRecipePreviewPage>();

		// Configure logging
		//var logPath = Path.Combine(FileSystem.CacheDirectory, "logs");
		var logPath = "C:\\Logs";
		
		try
		{
			if (!Directory.Exists(logPath))
			{
				Directory.CreateDirectory(logPath);
				Debug.WriteLine($"Created log directory: {logPath}");
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"ERROR creating log directory {logPath}: {ex.Message}");
			// Fallback to temp directory if C:\Logs fails
			logPath = Path.Combine(Path.GetTempPath(), "MealPlanOrganizerLogs");
			Directory.CreateDirectory(logPath);
			Debug.WriteLine($"Fallback log directory: {logPath}");
		}
		
		try
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.WriteTo.File(
					path: Path.Combine(logPath, "MealPlanOrganizer-.txt"),
					rollingInterval: RollingInterval.Day,
					fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
					retainedFileCountLimit: 5,
					outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
				)
				.CreateLogger();

			Log.Information("=== Meal Plan Organizer Started ===");
			Log.Information("Log directory: {LogPath}", logPath);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"ERROR configuring Serilog: {ex.Message}");
			throw;
		}

		builder.Logging.AddSerilog(Log.Logger);

		return builder.Build();
	}
}
