using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Diagnostics;
using MealPlanOrganizer.Mobile.Services;
using MealPlanOrganizer.Mobile.ViewModels;
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
		// For MAUI, config files must be loaded from the app package
		var config = new ConfigurationBuilder()
			.AddJsonStream(FileSystem.OpenAppPackageFileAsync("appsettings.json").Result)
			.AddJsonStream(FileSystem.OpenAppPackageFileAsync("appsettings.local.json").Result)
			.Build();
		
		builder.Configuration.AddConfiguration(config);

		// Register services
		builder.Services.AddHttpClient<RecipeService>();
		builder.Services.AddSingleton<IRecipeService>(sp => sp.GetRequiredService<RecipeService>());
		
		// Register user service
		builder.Services.AddHttpClient<UserService>();
		builder.Services.AddSingleton<IUserService>(sp => sp.GetRequiredService<UserService>());

		// Register authentication service
		builder.Services.AddSingleton<IAuthService, AuthService>();

		// Register navigation service
		builder.Services.AddSingleton<INavigationService, NavigationService>();

		// Register push notification service
		builder.Services.AddHttpClient<PushNotificationService>();
		builder.Services.AddSingleton<IPushNotificationService>(sp => sp.GetRequiredService<PushNotificationService>());

		// Register ViewModels
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<CreateHouseholdViewModel>();
		builder.Services.AddTransient<JoinHouseholdViewModel>();
		builder.Services.AddTransient<MealPlansViewModel>();
		builder.Services.AddTransient<MainViewModel>();
		builder.Services.AddTransient<RecipeEditorViewModel>();
		builder.Services.AddTransient<RecipeDetailViewModel>();
		builder.Services.AddTransient<MealPlanDetailViewModel>();
		builder.Services.AddTransient<RecipePickerPageViewModel>();
		builder.Services.AddTransient<CreateMealPlanViewModel>();
		builder.Services.AddTransient<ExtractRecipeViewModel>();
		builder.Services.AddTransient<ExtractedRecipePreviewViewModel>();
		builder.Services.AddTransient<ManageHouseholdViewModel>();

		// Register pages for dependency injection
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<CreateHouseholdPage>();
		builder.Services.AddTransient<JoinHouseholdPage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<MealPlansPage>();
		builder.Services.AddTransient<AddRecipePage>();
		builder.Services.AddTransient<EditRecipePage>();
		builder.Services.AddTransient<RecipeDetailPage>();
		builder.Services.AddTransient<MealPlanDetailPage>();
		builder.Services.AddTransient<ExtractRecipePage>();
		builder.Services.AddTransient<ExtractedRecipePreviewPage>();
		builder.Services.AddTransient<RecipePickerPage>();
		builder.Services.AddTransient<CreateMealPlanPage>();
		builder.Services.AddTransient<ManageHouseholdPage>();

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

		var app = builder.Build();
		
		Log.Information("MauiApp built successfully");
		
		return app;
	}
}
