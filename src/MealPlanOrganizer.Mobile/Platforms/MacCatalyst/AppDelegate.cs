using Foundation;
using MealPlanOrganizer.Mobile.Services;
using UIKit;

namespace MealPlanOrganizer.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
	
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
