using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Service for sending push notifications via Azure Notification Hubs.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Registers a device for push notifications.
    /// </summary>
    /// <param name="userId">The user's ID (used as tag).</param>
    /// <param name="platform">Platform: "ios", "android", or "windows".</param>
    /// <param name="pushToken">The device push token.</param>
    /// <returns>The Notification Hub registration ID.</returns>
    Task<string?> RegisterDeviceAsync(Guid userId, string platform, string pushToken);

    /// <summary>
    /// Unregisters a device from push notifications.
    /// </summary>
    /// <param name="registrationId">The Notification Hub registration ID.</param>
    Task UnregisterDeviceAsync(string registrationId);

    /// <summary>
    /// Sends a push notification to a specific user.
    /// </summary>
    /// <param name="userId">The user to notify.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification body.</param>
    /// <param name="data">Optional data payload.</param>
    Task SendToUserAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null);

    /// <summary>
    /// Sends a push notification to all users in a household.
    /// </summary>
    /// <param name="householdId">The household ID.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification body.</param>
    /// <param name="data">Optional data payload.</param>
    Task SendToHouseholdAsync(Guid householdId, string title, string body, Dictionary<string, string>? data = null);
}

/// <summary>
/// Implementation of INotificationService using Azure Notification Hubs.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly NotificationHubClient? _hubClient;
    private readonly ILogger<NotificationService> _logger;
    private readonly bool _isConfigured;

    public NotificationService(IConfiguration configuration, ILogger<NotificationService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration["NotificationHub:ConnectionString"];
        var hubName = configuration["NotificationHub:HubName"];

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(hubName))
        {
            _logger.LogWarning("Azure Notification Hubs is not configured. Push notifications will be disabled.");
            _isConfigured = false;
            return;
        }

        try
        {
            _hubClient = NotificationHubClient.CreateClientFromConnectionString(connectionString, hubName);
            _isConfigured = true;
            _logger.LogInformation("Azure Notification Hubs configured successfully for hub: {HubName}", hubName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Notification Hub client");
            _isConfigured = false;
        }
    }

    public async Task<string?> RegisterDeviceAsync(Guid userId, string platform, string pushToken)
    {
        if (!_isConfigured || _hubClient == null)
        {
            _logger.LogWarning("Notification Hub not configured, skipping device registration");
            return null;
        }

        try
        {
            // Tags for targeting: user-specific and household (added later via UpdateRegistrationAsync)
            var tags = new[] { $"userId:{userId}" };

            RegistrationDescription registration = platform.ToLowerInvariant() switch
            {
                "ios" => new AppleRegistrationDescription(pushToken, tags),
                "android" => new FcmRegistrationDescription(pushToken, tags),
                "windows" => new WindowsRegistrationDescription(pushToken, tags),
                _ => throw new ArgumentException($"Unsupported platform: {platform}")
            };

            var result = await _hubClient.CreateOrUpdateRegistrationAsync(registration);
            
            _logger.LogInformation("Device registered successfully. Platform: {Platform}, UserId: {UserId}, RegistrationId: {RegistrationId}",
                platform, userId, result.RegistrationId);
            
            return result.RegistrationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device for user {UserId}", userId);
            return null;
        }
    }

    public async Task UnregisterDeviceAsync(string registrationId)
    {
        if (!_isConfigured || _hubClient == null)
        {
            _logger.LogWarning("Notification Hub not configured, skipping device unregistration");
            return;
        }

        try
        {
            await _hubClient.DeleteRegistrationAsync(registrationId);
            _logger.LogInformation("Device unregistered successfully. RegistrationId: {RegistrationId}", registrationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister device with registrationId {RegistrationId}", registrationId);
        }
    }

    public async Task SendToUserAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null)
    {
        if (!_isConfigured || _hubClient == null)
        {
            _logger.LogWarning("Notification Hub not configured, skipping notification to user {UserId}", userId);
            return;
        }

        var tag = $"userId:{userId}";
        await SendNotificationAsync(tag, title, body, data);
    }

    public async Task SendToHouseholdAsync(Guid householdId, string title, string body, Dictionary<string, string>? data = null)
    {
        if (!_isConfigured || _hubClient == null)
        {
            _logger.LogWarning("Notification Hub not configured, skipping notification to household {HouseholdId}", householdId);
            return;
        }

        var tag = $"householdId:{householdId}";
        await SendNotificationAsync(tag, title, body, data);
    }

    private async Task SendNotificationAsync(string tagExpression, string title, string body, Dictionary<string, string>? data)
    {
        if (_hubClient == null) return;

        try
        {
            // Build data payload
            var payload = data ?? new Dictionary<string, string>();
            payload["title"] = title;
            payload["body"] = body;

            // Send to all platforms using template registration (cross-platform)
            // For now, we send to each platform separately with native payloads

            // iOS (APNs)
            var apnsPayload = CreateApnsPayload(title, body, payload);
            await _hubClient.SendAppleNativeNotificationAsync(apnsPayload, tagExpression);

            // Android (FCM)
            var fcmPayload = CreateFcmPayload(title, body, payload);
            await _hubClient.SendFcmNativeNotificationAsync(fcmPayload, tagExpression);

            // Windows (WNS) - Toast notification
            var wnsPayload = CreateWnsPayload(title, body);
            await _hubClient.SendWindowsNativeNotificationAsync(wnsPayload, tagExpression);

            _logger.LogInformation("Notification sent to tag: {Tag}, Title: {Title}", tagExpression, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to tag {Tag}", tagExpression);
        }
    }

    private static string CreateApnsPayload(string title, string body, Dictionary<string, string> data)
    {
        // APNs payload format
        var dataJson = System.Text.Json.JsonSerializer.Serialize(data);
        return $$"""
        {
            "aps": {
                "alert": {
                    "title": "{{EscapeJson(title)}}",
                    "body": "{{EscapeJson(body)}}"
                },
                "sound": "default",
                "badge": 1
            },
            "data": {{dataJson}}
        }
        """;
    }

    private static string CreateFcmPayload(string title, string body, Dictionary<string, string> data)
    {
        // FCM v1 payload format
        var dataJson = System.Text.Json.JsonSerializer.Serialize(data);
        return $$"""
        {
            "message": {
                "notification": {
                    "title": "{{EscapeJson(title)}}",
                    "body": "{{EscapeJson(body)}}"
                },
                "data": {{dataJson}}
            }
        }
        """;
    }

    private static string CreateWnsPayload(string title, string body)
    {
        // WNS Toast notification XML
        return $"""
        <toast>
            <visual>
                <binding template="ToastText02">
                    <text id="1">{System.Security.SecurityElement.Escape(title)}</text>
                    <text id="2">{System.Security.SecurityElement.Escape(body)}</text>
                </binding>
            </visual>
        </toast>
        """;
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
