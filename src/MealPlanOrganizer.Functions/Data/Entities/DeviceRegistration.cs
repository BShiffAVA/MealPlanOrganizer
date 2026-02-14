namespace MealPlanOrganizer.Functions.Data.Entities;

/// <summary>
/// Tracks a registered device for push notifications.
/// </summary>
public class DeviceRegistration
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The user this device belongs to.
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Platform: "ios", "android", or "windows".
    /// </summary>
    public string Platform { get; set; } = string.Empty;
    
    /// <summary>
    /// Device push token (APNs token, FCM token, or WNS channel URI).
    /// </summary>
    public string PushToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure Notification Hub registration ID (from the hub).
    /// </summary>
    public string? NotificationHubRegistrationId { get; set; }
    
    /// <summary>
    /// When this device was registered.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
    
    /// <summary>
    /// When the registration was last updated.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }
    
    /// <summary>
    /// Whether this device is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public User? User { get; set; }
}
