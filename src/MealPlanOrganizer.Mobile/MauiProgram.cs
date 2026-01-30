using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using MealPlanOrganizer.Mobile.Services;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
