namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service for managing push notification device registration with the backend.
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Gets or sets whether push notifications are enabled by the user.
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// Gets the current device's push token (platform-specific).
    /// </summary>
    string? CurrentPushToken { get; }
    
    /// <summary>
    /// Gets the current platform identifier (ios, android, windows).
    /// </summary>
    string Platform { get; }
    
    /// <summary>
    /// Initializes push notification services for the current platform.
    /// Should be called on app startup.
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Registers the device with the backend for push notifications.
    /// This should be called after successful user authentication.
    /// </summary>
    /// <returns>True if registration was successful.</returns>
    Task<bool> RegisterDeviceAsync();
    
    /// <summary>
    /// Unregisters the device from push notifications.
    /// Should be called when the user logs out.
    /// </summary>
    /// <returns>True if unregistration was successful.</returns>
    Task<bool> UnregisterDeviceAsync();
    
    /// <summary>
    /// Checks if push notifications are available on this device.
    /// </summary>
    bool IsSupported { get; }
    
    /// <summary>
    /// Event raised when a push notification is received while the app is in the foreground.
    /// </summary>
    event EventHandler<PushNotificationReceivedEventArgs>? NotificationReceived;
}

/// <summary>
/// Event arguments for when a push notification is received.
/// </summary>
public class PushNotificationReceivedEventArgs : EventArgs
{
    /// <summary>
    /// The notification title.
    /// </summary>
    public string? Title { get; init; }
    
    /// <summary>
    /// The notification body/message.
    /// </summary>
    public string? Body { get; init; }
    
    /// <summary>
    /// Additional data payload from the notification.
    /// </summary>
    public Dictionary<string, string>? Data { get; init; }
}
