using Foundation;
using MealPlanOrganizer.Mobile.Services;
using UIKit;

namespace MealPlanOrganizer.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
