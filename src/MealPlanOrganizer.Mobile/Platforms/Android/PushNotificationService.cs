using Microsoft.Extensions.Logging;
using Plugin.Firebase.CloudMessaging;
using Firebase;
using MealPlanOrganizer.Mobile.Platforms.Android;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Android-specific implementation of push notification initialization.
/// Uses Firebase Cloud Messaging (FCM) for push notification delivery.
/// </summary>
/// <remarks>
/// Prerequisites for FCM:
/// 1. Add google-services.json to Platforms/Android/
/// 2. Install Xamarin.Firebase.Messaging NuGet package
/// 3. Configure Firebase project in Firebase Console
/// 4. Add the FirebaseMessagingService to handle messages (MealPlanFirebaseMessagingService.cs)
/// </remarks>
public partial class PushNotificationService
{
    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (!IsSupported)
        {
            _logger.LogWarning("Push notifications not supported on this platform");
            return;
        }

        EnablePushNotifications();

        if (!IsEnabled)
        {
            _logger.LogInformation("Push notifications disabled by user");
            return;
        }

        try
        {
            _logger.LogInformation("Initializing Android push notifications (FCM)");
            
            // Get the current FCM token using Plugin.Firebase
            // Note: Firebase must be initialized before this point (see MainApplication.OnCreate)
            var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("FCM token obtained successfully: {Token}", token);
                SetPushToken(token);
            }
            else
            {
                _logger.LogWarning("Failed to obtain FCM token - token is empty");
            }
            
            _logger.LogInformation("Android push notification initialization complete");
        }
        catch (Java.Lang.IllegalStateException ex) when (ex.Message?.Contains("Default FirebaseApp") == true)
        {
            _logger.LogError(ex, "Firebase not initialized. Attempting delayed initialization...");
            // Try to initialize Firebase here as a fallback
            try
            {
                // Try to get a proper Android context - use the platform's context
                var context = Android.App.Application.Context ?? 
                             (Android.Content.Context)Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Context;
                
                if (context == null)
                {
                    _logger.LogError("Cannot obtain Android context - Firebase initialization failed");
                    return;
                }
                
                _logger.LogInformation("Initializing Firebase with context type: {ContextType}", context.GetType().Name);
                
                try
                {
                    // Clear any existing incomplete initialization
                    FirebaseApp.InitializeApp(context);
                }
                catch
                {
                    // If initialization throws, it might already be initialized
                    _logger.LogInformation("Firebase.InitializeApp threw an exception, checking if already initialized...");
                }
                
                // Give Firebase initialization time to complete
                await Task.Delay(1000);
                
                _logger.LogInformation("Firebase initialization attempt complete");
                
                // Retry getting token after initialization
                try
                {
                    var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                        _logger.LogInformation("FCM token obtained after fallback initialization");
                        SetPushToken(token);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to obtain FCM token after fallback initialization");
                    }
                }
                catch (Java.Lang.IllegalStateException tokenEx)
                {
                    _logger.LogError(tokenEx, "Still cannot get token - Firebase may not be properly initialized");
                }
            }
            catch (Exception initEx)
            {
                _logger.LogError(initEx, "Failed to initialize Firebase via fallback method");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Android push notifications");
        }
    }

    private async Task EnablePushNotifications()
    {

        var granted = await NotificationPermission.EnsureAsync();
        if (!granted)
        {
            await Shell.Current.DisplayAlert(
                "Notifications disabled",
                "You can enable notifications anytime in Settings > Apps > Meal Plan Organizer > Notifications.",
                "OK");
            return;
        }

    }

    /// <summary>
    /// Called when FCM token is refreshed. This should be called from a FirebaseMessagingService.
    /// </summary>
    internal void OnTokenRefresh(string? newToken)
    {
        if (!string.IsNullOrEmpty(newToken))
        {
            _logger.LogInformation("FCM token refreshed");
            SetPushToken(newToken);
            
            // Re-register with backend
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await RegisterDeviceAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to re-register after token refresh");
                }
            });
        }
    }

    /// <summary>
    /// Handles incoming FCM message when app is in foreground.
    /// This should be called from a FirebaseMessagingService.
    /// </summary>
    internal void OnMessageReceived(Dictionary<string, string> data, string? title, string? body)
    {
        _logger.LogInformation("FCM message received in foreground: {Title}", title ?? "(no title)");

        OnNotificationReceived(new Services.PushNotificationReceivedEventArgs
        {
            Title = title,
            Body = body,
            Data = data
        });
    }
}
