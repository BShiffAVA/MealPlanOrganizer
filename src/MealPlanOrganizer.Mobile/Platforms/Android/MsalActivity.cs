using Android.App;
using Android.Content;
using Microsoft.Identity.Client;

namespace MealPlanOrganizer.Mobile.Platforms.Android;

/// <summary>
/// Activity that handles the redirect from the browser after MSAL authentication.
/// The DataScheme must match the redirect URI configured in Azure AD: msal{ClientId}://auth
/// </summary>
[Activity(Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryBrowsable, Intent.CategoryDefault],
    DataHost = "auth",
    DataScheme = "msal8d3adb68-f2c6-474b-a96b-375a30cacd8a")]
public class MsalActivity : BrowserTabActivity
{
}
