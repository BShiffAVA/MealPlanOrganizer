using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MealPlanOrganizer.Mobile;

public partial class AppShell : Shell
{
	private readonly IAuthService _authService;
	private readonly IPushNotificationService _pushNotificationService;

	public AppShell()
	{
		InitializeComponent();
		
		// Get auth service from DI
		_authService = Application.Current?.Handler?.MauiContext?.Services.GetService<IAuthService>()
			?? throw new InvalidOperationException("IAuthService not registered");

		// Get push notification service from DI
		_pushNotificationService = Application.Current?.Handler?.MauiContext?.Services.GetService<IPushNotificationService>()
			?? throw new InvalidOperationException("IPushNotificationService not registered");

		// Resolve pages from DI for ShellContent that require constructor injection
		var services = Application.Current?.Handler?.MauiContext?.Services;
		if (services != null)
		{
			HomeShellContent.Content = services.GetRequiredService<MainPage>();
			MealPlansShellContent.Content = services.GetRequiredService<MealPlansPage>();
		}
		
		// Register routes for programmatic navigation via GoToAsync()
		// NOTE: ShellContent tabs use unique Route names in XAML (HomeTab, MealPlansTab, etc.)
		// to avoid conflicts with these navigation routes.
		Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
		Routing.RegisterRoute(nameof(EditRecipePage), typeof(EditRecipePage));
		Routing.RegisterRoute(nameof(AddRecipePage), typeof(AddRecipePage));
		Routing.RegisterRoute(nameof(ExtractRecipePage), typeof(ExtractRecipePage));
		Routing.RegisterRoute(nameof(ExtractedRecipePreviewPage), typeof(ExtractedRecipePreviewPage));
		Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
		Routing.RegisterRoute(nameof(CreateHouseholdPage), typeof(CreateHouseholdPage));
		
		// Meal plan routes
		Routing.RegisterRoute(nameof(CreateMealPlanPage), typeof(CreateMealPlanPage));
		Routing.RegisterRoute(nameof(MealPlanDetailPage), typeof(MealPlanDetailPage));
		Routing.RegisterRoute(nameof(RecipePickerPage), typeof(RecipePickerPage));
		
		// Household management route
		Routing.RegisterRoute(nameof(ManageHouseholdPage), typeof(ManageHouseholdPage));

		// Subscribe to navigation events for logging and tab switch handling
		Navigating += OnNavigating;
		Navigated += OnNavigated;
	}

	private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		Log.Debug("Shell navigating from {Current} to {Target} (Source: {Source})", 
			e.Current?.Location, e.Target?.Location, e.Source);
		
		// When clicking a tab while on a pushed page, Shell navigates to the same location
		// instead of popping to root. Detect and fix this.
		var currentPath = e.Current?.Location?.ToString() ?? "";
		var targetPath = e.Target?.Location?.ToString() ?? "";
		
		if (currentPath == targetPath && currentPath.Contains("/"))
		{
			// We're on a pushed page and trying to go to the same place - pop to tab root instead
			var segments = currentPath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
			if (segments.Length > 1) // More than just the tab root
			{
				e.Cancel();
				Log.Debug("Popping to root from pushed page stack");
				
				// Defer the pop to avoid conflict with the cancelled event
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					try
					{
						// Pop all pages to get back to the root of current tab
						await Shell.Current.Navigation.PopToRootAsync(true);
					}
					catch (Exception ex)
					{
						Log.Error(ex, "Failed to pop to root");
					}
				});
			}
		}
	}

	private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		Log.Debug("Shell navigated to {Current} from {Previous} (Source: {Source})", 
			e.Current?.Location, e.Previous?.Location, e.Source);
	}

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		bool confirm = await DisplayAlertAsync(
			"Sign Out",
			"Are you sure you want to sign out?",
			"Sign Out",
			"Cancel");

		if (confirm)
		{
			await LogoutAsync();
		}
	}

	private async void OnManageHouseholdClicked(object? sender, EventArgs e)
	{
		try
		{
			await Shell.Current.GoToAsync(nameof(ManageHouseholdPage));
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to navigate to Manage Household");
			await DisplayAlertAsync("Error", "Failed to open Manage Household page.", "OK");
		}
	}

	/// <summary>
	/// Logs the user out and navigates back to the login page.
	/// </summary>
	private async Task LogoutAsync()
	{
		try
		{
			// Unregister device from push notifications before logout
			try
			{
				await _pushNotificationService.UnregisterDeviceAsync();
				Log.Information("Device unregistered from push notifications");
			}
			catch (Exception ex)
			{
				// Don't block logout if push unregistration fails
				Log.Warning(ex, "Failed to unregister device from push notifications");
			}
			
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
			Log.Error(ex, "Logout error");
			await DisplayAlertAsync("Error", "Failed to sign out. Please try again.", "OK");
		}
	}
}
