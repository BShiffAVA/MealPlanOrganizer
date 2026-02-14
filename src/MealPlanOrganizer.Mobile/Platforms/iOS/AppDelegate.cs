using Foundation;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Identity.Client;
using UIKit;

namespace MealPlanOrganizer.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Handle MSAL authentication callback
	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (AuthenticationContinuationHelper.IsBrokerResponse(null))
		{
			AuthenticationContinuationHelper.SetBrokerContinuationEventArgs(url);
			return true;
		}

		AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(url);
		return base.OpenUrl(application, url, options);
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
