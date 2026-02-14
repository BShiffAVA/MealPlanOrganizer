using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;
using UserNotifications;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// MacCatalyst-specific implementation of push notification initialization.
/// Uses Apple Push Notification Service (APNs) for push notification delivery.
/// Shares much of the same API as iOS.
/// </summary>
public partial class PushNotificationService
{
    private static IPushNotificationService? _instance;
    private MacPushNotificationDelegate? _notificationDelegate;
    
    /// <summary>
    /// Gets the singleton instance for delegate callbacks.
    /// </summary>
    internal static PushNotificationService? Instance => _instance as PushNotificationService;
    
    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (!IsSupported)
        {
            _logger.LogWarning("Push notifications not supported on this platform");
            return;
        }

        if (!IsEnabled)
        {
            _logger.LogInformation("Push notifications disabled by user");
            return;
        }

        try
        {
            _logger.LogInformation("Initializing MacCatalyst push notifications (APNs)");
            
            _instance = this;

            // Request notification permission
            var center = UNUserNotificationCenter.Current;
            _notificationDelegate = new MacPushNotificationDelegate(this);
            center.Delegate = _notificationDelegate;

            var (granted, error) = await center.RequestAuthorizationAsync(
                UNAuthorizationOptions.Alert | 
                UNAuthorizationOptions.Badge | 
                UNAuthorizationOptions.Sound);

            if (error != null)
            {
                _logger.LogError("Error requesting push notification authorization: {Error}", error.LocalizedDescription);
                return;
            }

            if (!granted)
            {
                _logger.LogWarning("Push notification permission denied by user");
                return;
            }

            _logger.LogInformation("Push notification permission granted");

            // Register for remote notifications - must be done on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UIApplication.SharedApplication.RegisterForRemoteNotifications();
            });

            _logger.LogInformation("MacCatalyst push notification initialization complete - waiting for APNs token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MacCatalyst push notifications");
        }
    }

    /// <summary>
    /// Called by AppDelegate when APNs device token is received.
    /// </summary>
    internal void OnRegisteredForRemoteNotifications(NSData deviceToken)
    {
        // Convert token data to hex string
        var tokenBytes = new byte[deviceToken.Length];
        System.Runtime.InteropServices.Marshal.Copy(deviceToken.Bytes, tokenBytes, 0, (int)deviceToken.Length);
        var token = BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();

        _logger.LogInformation("APNs device token received");
        _logger.LogDebug("APNs Token: {Token}", token);

        SetPushToken(token);
    }

    /// <summary>
    /// Called by AppDelegate when APNs registration fails.
    /// </summary>
    internal void OnFailedToRegisterForRemoteNotifications(NSError error)
    {
        _logger.LogError("Failed to register for APNs: {Error}", error.LocalizedDescription);
    }

    /// <summary>
    /// Handles notification presentation when app is in foreground.
    /// </summary>
    internal void HandleNotificationPresentation(UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
    {
        var content = notification.Request.Content;
        var userInfo = content.UserInfo;

        _logger.LogInformation("Push notification received in foreground: {Title}", content.Title ?? "(no title)");

        // Convert user info to dictionary
        var data = new Dictionary<string, string>();
        foreach (var key in userInfo.Keys)
        {
            var keyString = key.ToString();
            var value = userInfo[key]?.ToString();
            if (!string.IsNullOrEmpty(keyString) && !string.IsNullOrEmpty(value))
            {
                data[keyString] = value;
            }
        }

        OnNotificationReceived(new Services.PushNotificationReceivedEventArgs
        {
            Title = content.Title,
            Body = content.Body,
            Data = data
        });

        // Show banner, sound, and badge even when app is in foreground
        completionHandler(UNNotificationPresentationOptions.Banner | 
                         UNNotificationPresentationOptions.Sound | 
                         UNNotificationPresentationOptions.Badge);
    }

    /// <summary>
    /// Handles user interaction with notification.
    /// </summary>
    internal void HandleNotificationResponse(UNNotificationResponse response, Action completionHandler)
    {
        var content = response.Notification.Request.Content;
        _logger.LogInformation("User interacted with notification: {Title}", content.Title ?? "(no title)");
        completionHandler();
    }
}

/// <summary>
/// Delegate class to handle push notification callbacks on MacCatalyst.
/// </summary>
internal class MacPushNotificationDelegate : UNUserNotificationCenterDelegate
{
    private readonly PushNotificationService _service;

    public MacPushNotificationDelegate(PushNotificationService service)
    {
        _service = service;
    }

    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
    {
        _service.HandleNotificationPresentation(notification, completionHandler);
    }

    public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        _service.HandleNotificationResponse(response, completionHandler);
    }
}
