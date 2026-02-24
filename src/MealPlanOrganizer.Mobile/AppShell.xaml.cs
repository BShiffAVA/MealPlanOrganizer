using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MealPlanOrganizer.Mobile;

public partial class AppShell : Shell
{
	private readonly IAuthService _authService;
	private readonly IPushNotificationService _pushNotificationService;
	private readonly IDeepLinkService _deepLinkService;
	private readonly IAppStartupService _appStartupService;

	public AppShell()
	{
		InitializeComponent();
		
		// Get services from DI
		var services = Application.Current?.Handler?.MauiContext?.Services;
		if (services == null)
		{
			throw new InvalidOperationException("Services not available");
		}

		_authService = services.GetService<IAuthService>()
			?? throw new InvalidOperationException("IAuthService not registered");

		_pushNotificationService = services.GetService<IPushNotificationService>()
			?? throw new InvalidOperationException("IPushNotificationService not registered");

		_deepLinkService = services.GetService<IDeepLinkService>()
			?? throw new InvalidOperationException("IDeepLinkService not registered");

		_appStartupService = services.GetService<IAppStartupService>()
			?? throw new InvalidOperationException("IAppStartupService not registered");

		// Resolve pages from DI for ShellContent
		HomeShellContent.Content = services.GetRequiredService<MainPage>();
		MealPlansShellContent.Content = services.GetRequiredService<MealPlansPage>();
		
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
		
		// Rating reminder route (for push notification deep links)
		Routing.RegisterRoute(nameof(QuickRateRecipePage), typeof(QuickRateRecipePage));

		// Subscribe to push notification events for deep link navigation (foreground notifications)
		_pushNotificationService.NotificationReceived += OnPushNotificationReceived;

		// Subscribe to navigation events for logging and tab switch handling
		Navigating += OnNavigating;
		Navigated += OnNavigated;

		// Subscribe to window lifecycle events for pending ratings check on resume
		var window = Application.Current?.Windows.FirstOrDefault();
		if (window != null)
		{
			window.Resumed += OnShellResumed;
			window.Stopped += OnShellStopped;
		}

		// Process any pending deep link actions after Shell is fully loaded
		Loaded += OnShellLoaded;
	}

	/// <summary>
	/// Handles window stopped event (app going to background).
	/// </summary>
	private void OnShellStopped(object? sender, EventArgs e)
	{
		Log.Information("Shell going to background");
		
		try
		{
			// Reset the startup check flag for next resume
			_appStartupService.HasPerformedStartupCheck = false;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error handling shell stop");
		}
	}

	/// <summary>
	/// Handles Shell loaded event to process any pending deep link actions
	/// and check for pending ratings on app open.
	/// </summary>
	private async void OnShellLoaded(object? sender, EventArgs e)
	{
		try
		{
			// Small delay to ensure Shell is fully initialized and navigable
			await Task.Delay(100);
			
			// Process any pending deep link action from cold start or pre-auth notification
			// Check both instance-level pending action and static flag (set by Android intent handler)
			var hadPendingAction = _deepLinkService.PendingAction != null 
				|| DeepLinkService.HasPendingDeepLinkNotification 
				|| _deepLinkService.IsProcessingDeepLink;
				
			await _deepLinkService.ProcessPendingActionAsync();
			
			// Check for pending ratings on app startup (Step 8 of rate recipes feature)
			// Only prompt if the user wasn't already navigated by a deep link
			if (!hadPendingAction)
			{
				await _appStartupService.CheckPendingRatingsAsync();
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error in shell loaded handler");
		}
	}

	/// <summary>
	/// Handles Shell resumed event to check for pending ratings when app comes back from background.
	/// Coordinates with deep links to avoid showing ratings popup when user arrives via deep link.
	/// </summary>
	private async void OnShellResumed(object? sender, EventArgs e)
	{
		Log.Information("Shell resumed from background");

		try
		{
			// Wait for Android intent handling to complete - OnNewIntent runs after Window.Resumed
			// so we need to give it time to set the deep link flags
			await Task.Delay(700);
			
			// Check if there's a pending deep link action from a notification click
			// Check both instance-level pending action and static flag (set by Android intent handler)
			var hadPendingAction = _deepLinkService.PendingAction != null 
				|| DeepLinkService.HasPendingDeepLinkNotification 
				|| _deepLinkService.IsProcessingDeepLink;
			
			// Process any pending deep link
			await _deepLinkService.ProcessPendingActionAsync();
			
			// Only check for pending ratings if the user wasn't navigated by a deep link
			if (!hadPendingAction)
			{
				await _appStartupService.CheckPendingRatingsAsync();
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error in shell resumed handler");
		}
	}

	/// <summary>
	/// Handles push notification received event for deep link navigation (foreground only).
	/// </summary>
	private async void OnPushNotificationReceived(object? sender, PushNotificationReceivedEventArgs e)
	{
		try
		{
			Log.Information("Push notification received: {Title}", e.Title);
			
			// Use DeepLinkService to parse and handle the notification
			var action = _deepLinkService.ParseNotificationData(e.Data);
			if (action != null)
			{
				await _deepLinkService.ProcessDeepLinkAsync(action);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error handling push notification");
		}
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
