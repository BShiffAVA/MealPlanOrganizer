using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class AppShell : Shell
{
	private readonly IAuthService _authService;

	public AppShell()
	{
		InitializeComponent();
		
		// Get auth service from DI
		_authService = Application.Current?.Handler?.MauiContext?.Services.GetService<IAuthService>()
			?? throw new InvalidOperationException("IAuthService not registered");
		
		// Register routes for navigation
		Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
		Routing.RegisterRoute(nameof(EditRecipePage), typeof(EditRecipePage));
		Routing.RegisterRoute(nameof(ExtractRecipePage), typeof(ExtractRecipePage));
		Routing.RegisterRoute(nameof(ExtractedRecipePreviewPage), typeof(ExtractedRecipePreviewPage));
		Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
		
		// Meal plan routes
		Routing.RegisterRoute(nameof(CreateMealPlanPage), typeof(CreateMealPlanPage));
		Routing.RegisterRoute(nameof(MealPlanDetailPage), typeof(MealPlanDetailPage));
		Routing.RegisterRoute(nameof(RecipePickerPage), typeof(RecipePickerPage));
	}

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		bool confirm = await DisplayAlert(
			"Sign Out",
			"Are you sure you want to sign out?",
			"Sign Out",
			"Cancel");

		if (confirm)
		{
			await LogoutAsync();
		}
	}

	/// <summary>
	/// Logs the user out and navigates back to the login page.
	/// </summary>
	private async Task LogoutAsync()
	{
		try
		{
			await _authService.LogoutAsync();
			
			// Navigate back to login page
			if (Application.Current?.Windows.Count > 0)
			{
				var loginPage = Application.Current.Handler?.MauiContext?.Services.GetService<LoginPage>();
				if (loginPage != null)
				{
					Application.Current.Windows[0].Page = new NavigationPage(loginPage);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
			await DisplayAlert("Error", "Failed to sign out. Please try again.", "OK");
		}
	}
}
