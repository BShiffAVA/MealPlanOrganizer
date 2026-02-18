using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Maui.Controls;
using Serilog;

namespace MealPlanOrganizer.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme", 
    MainLauncher = true, 
    LaunchMode = LaunchMode.SingleTop, 
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "mealplanorganizer")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // Handle deep link if app was launched from notification tap
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        
        // Handle deep link when app is brought to foreground from notification tap
        if (intent != null)
        {
            HandleIntent(intent);
        }
    }

    private void HandleIntent(Intent? intent)
    {
        if (intent == null) return;

        try
        {
            // Check for notification data extras
            var extras = intent.Extras;
            if (extras != null)
            {
                var action = extras.GetString("action");
                if (!string.IsNullOrEmpty(action))
                {
                    Log.Information("Android intent received with action: {Action}", action);
                    
                    var data = new Dictionary<string, string>();
                    foreach (var key in extras.KeySet() ?? Enumerable.Empty<string>())
                    {
                        var value = extras.GetString(key);
                        if (!string.IsNullOrEmpty(value))
                        {
                            data[key] = value;
                        }
                    }

                    ProcessDeepLinkData(data);
                    return;
                }
            }

            // Check for URI scheme deep link
            var dataUri = intent.Data;
            if (dataUri != null)
            {
                var uri = dataUri.ToString();
                Log.Information("Android intent received with URI: {Uri}", uri);
                
                if (!string.IsNullOrEmpty(uri))
                {
                    ProcessDeepLinkUri(uri);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling Android intent");
        }
    }

    private void ProcessDeepLinkData(Dictionary<string, string> data)
    {
        // Get the deep link service and process the notification data
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // Wait a brief moment for the app to fully initialize
                await Task.Delay(500);

                var services = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
                var deepLinkService = services?.GetService<IDeepLinkService>();

                if (deepLinkService != null)
                {
                    var action = deepLinkService.ParseNotificationData(data);
                    if (action != null)
                    {
                        // Check if Shell is ready
                        if (Shell.Current != null)
                        {
                            await deepLinkService.ProcessDeepLinkAsync(action);
                        }
                        else
                        {
                            // Store for later processing after login
                            deepLinkService.PendingAction = action;
                            Log.Information("Deep link action queued for after authentication");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing deep link data");
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

                var services = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
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
                            Log.Information("URI deep link action queued for after authentication");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing deep link URI");
            }
        });
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        // Forward result to MSAL for authentication handling
        AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data);
    }
}

