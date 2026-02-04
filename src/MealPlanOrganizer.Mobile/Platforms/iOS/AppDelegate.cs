using Foundation;
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
}
