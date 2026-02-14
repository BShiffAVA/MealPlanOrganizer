using Foundation;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Serilog;
using UIKit;
using UserNotifications;

namespace MealPlanOrganizer.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate, IUNUserNotificationCenterDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		// Set up notification center delegate to handle notification taps
		UNUserNotificationCenter.Current.Delegate = this;

		// Check if app was launched from a notification
		if (launchOptions != null)
		{
			if (launchOptions.TryGetValue(UIApplication.LaunchOptionsRemoteNotificationKey, out var notification))
			{
				if (notification is NSDictionary notificationData)
				{
					HandleNotificationData(notificationData);
				}
			}
		}

		return base.FinishedLaunching(application, launchOptions);
	}

	// Handle MSAL authentication callback and custom URI scheme deep links
	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		var urlString = url?.ToString();
		Log.Information("iOS OpenUrl called with: {Url}", urlString);

		// Check for MSAL broker response
		if (AuthenticationContinuationHelper.IsBrokerResponse(null))
		{
			AuthenticationContinuationHelper.SetBrokerContinuationEventArgs(url);
			return true;
		}

		// Check for our custom URI scheme
		if (!string.IsNullOrEmpty(urlString) && urlString.StartsWith("mealplanorganizer://", StringComparison.OrdinalIgnoreCase))
		{
			ProcessDeepLinkUri(urlString);
			return true;
		}

		// Forward to MSAL for authentication
		AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(url);
		return base.OpenUrl(application, url, options);
	}

	// Handle notification tap when app is in foreground
	[Export("userNotificationCenter:willPresentNotification:withCompletionHandler:")]
	public void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
	{
		Log.Information("iOS notification received in foreground");
		
		// Show banner, play sound, update badge
		completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound);
	}

	// Handle notification tap (both background and foreground)
	[Export("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:")]
	public void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
	{
		try
		{
			Log.Information("iOS notification tapped");
			
			var userInfo = response.Notification.Request.Content.UserInfo;
			if (userInfo != null)
			{
				HandleNotificationData(userInfo);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error handling iOS notification response");
		}
		finally
		{
			completionHandler();
		}
	}

	private void HandleNotificationData(NSDictionary userInfo)
	{
		try
		{
			var data = new Dictionary<string, string>();
			
			foreach (var key in userInfo.Keys)
			{
				var keyString = key.ToString();
				var value = userInfo[key];
				if (value != null)
				{
					data[keyString] = value.ToString();
				}
			}

			// Check nested 'aps' dictionary for custom data
			if (userInfo.TryGetValue(new NSString("aps"), out var apsObj) && apsObj is NSDictionary aps)
			{
				foreach (var key in aps.Keys)
				{
					var keyString = key.ToString();
					var value = aps[key];
					if (value != null && !data.ContainsKey(keyString))
					{
						data[keyString] = value.ToString();
					}
				}
			}

			if (data.ContainsKey("action"))
			{
				Log.Information("iOS notification with action: {Action}", data["action"]);
				ProcessDeepLinkData(data);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error parsing iOS notification data");
		}
	}

	private void ProcessDeepLinkData(Dictionary<string, string> data)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			try
			{
				await Task.Delay(500);

				var services = Application.Current?.Handler?.MauiContext?.Services;
				var deepLinkService = services?.GetService<IDeepLinkService>();

				if (deepLinkService != null)
				{
					var action = deepLinkService.ParseNotificationData(data);
					if (action != null)
					{
						if (Shell.Current != null)
						{
							await deepLinkService.ProcessDeepLinkAsync(action);
						}
						else
						{
							deepLinkService.PendingAction = action;
							Log.Information("iOS deep link action queued for after authentication");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error processing iOS deep link data");
			}
		});
	}

	private void ProcessDeepLinkUri(string uri)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			try
			{
				await Task.Delay(500);

				var services = Application.Current?.Handler?.MauiContext?.Services;
				var deepLinkService = services?.GetService<IDeepLinkService>();

				if (deepLinkService != null)
				{
					var action = deepLinkService.ParseUri(uri);
					if (action != null)
					{
						if (Shell.Current != null)
						{
							await deepLinkService.ProcessDeepLinkAsync(action);
						}
						else
						{
							deepLinkService.PendingAction = action;
							Log.Information("iOS URI deep link action queued for after authentication");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error processing iOS deep link URI");
			}
		});
	}

	// Handle APNs device token registration
	public override void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
	{
		PushNotificationService.Instance?.OnRegisteredForRemoteNotifications(deviceToken);
	}

	// Handle APNs registration failure
	public override void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
	{
		PushNotificationService.Instance?.OnFailedToRegisterForRemoteNotifications(error);
	}
}
