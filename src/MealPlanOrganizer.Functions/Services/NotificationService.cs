using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.NotificationHubs.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Service for sending push notifications via Azure Notification Hubs.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Registers a device for push notifications using the Installation API.
    /// </summary>
    /// <param name="installationId">A stable unique device identifier.</param>
    /// <param name="userId">The user's ID (used as tag).</param>
    /// <param name="platform">Platform: "ios", "android", or "windows".</param>
    /// <param name="pushToken">The device push token from FCM/APNs/WNS.</param>
    /// <returns>The installation ID on success, null on failure.</returns>
    Task<string?> RegisterDeviceAsync(string installationId, Guid userId, String platform, string pushToken);

    /// <summary>
    /// Unregisters a device from push notifications.
    /// </summary>
    /// <param name="installationId">The installation ID of the device to unregister.</param>
    Task UnregisterDeviceAsync(string installationId);

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
/// Implementation of INotificationService using Azure Notification Hubs Installation API.
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

    public async Task<string?> RegisterDeviceAsync(string installationId, Guid userId, String platform, string pushToken)
    {
        if (!_isConfigured || _hubClient == null)
        {
            _logger.LogWarning("Notification Hub not configured, skipping device registration");
            return null;
        }

        try
        {
            // Map platform string to NotificationPlatform enum
            var notificationPlatform = platform.ToLowerInvariant() switch
            {
                "ios" => NotificationPlatform.Apns,
                "android" => NotificationPlatform.FcmV1,
                "windows" => NotificationPlatform.Wns,
                _ => throw new ArgumentException($"Unsupported platform: {platform}")
            };

            // Create installation with tags for targeting
            var installation = new Installation
            {
                InstallationId = installationId,
                Platform = notificationPlatform,
                PushChannel = pushToken,
                Tags = new List<string> { $"userId:{userId}" }
            };

            // CreateOrUpdateInstallationAsync is async - it returns immediately but processes in background
            await _hubClient.CreateOrUpdateInstallationAsync(installation);
            
            // Verify the installation was actually created (helps diagnose silent failures)
            // Wait briefly for async processing, then check
            await Task.Delay(500);
            try
            {
                var verifyInstallation = await _hubClient.GetInstallationAsync(installationId);
                _logger.LogInformation(
                    "Device installation verified. Platform: {Platform}, UserId: {UserId}, InstallationId: {InstallationId}, PushChannel: {PushChannel}",
                    platform, userId, installationId, verifyInstallation.PushChannel?[..Math.Min(20, verifyInstallation.PushChannel.Length)] + "...");
            }
            catch (Exception verifyEx)
            {
                _logger.LogWarning(verifyEx, 
                    "Installation was submitted but could not be verified. This may indicate FCM/APNs credentials are not configured in the Notification Hub. InstallationId: {InstallationId}",
                    installationId);
            }
            
            return installationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device installation for user {UserId}", userId);
            return null;
        }
    }

    public async Task UnregisterDeviceAsync(string installationId)
    {
        if (!_isConfigured || _hubClient == null)
        {
            _logger.LogWarning("Notification Hub not configured, skipping device unregistration");
            return;
        }

        try
        {
            await _hubClient.DeleteInstallationAsync(installationId);
            _logger.LogInformation("Device installation unregistered successfully. InstallationId: {InstallationId}", installationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister device with installationId {InstallationId}", installationId);
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
            try
            {
                var apnsPayload = CreateApnsPayload(title, body, payload);
                await _hubClient.SendAppleNativeNotificationAsync(apnsPayload, tagExpression);
                _logger.LogInformation("APNs payload sent: {Payload} with tag {Tag}", apnsPayload, tagExpression);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to send iOS APNs notification to tag {Tag}", tagExpression);
            }

            // Android (FCM)
            try
            {
                var fcmPayload = CreateFcmPayload(title, body, payload);
                await _hubClient.SendFcmV1NativeNotificationAsync(fcmPayload, tagExpression);
                _logger.LogInformation("FCM payload sent: {Payload} with tag {Tag}", fcmPayload, tagExpression);
            }
            catch (Exception ex)            {
                _logger.LogError(ex, "Failed to send Android FCM notification to tag {Tag}", tagExpression);
            }

            // Windows (WNS) - Toast notification
            try
            {
                var wnsPayload = CreateWnsPayload(title, body);
                await _hubClient.SendWindowsNativeNotificationAsync(wnsPayload, tagExpression);
                _logger.LogInformation("WNS payload sent: {Payload} with tag {Tag}", wnsPayload, tagExpression);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Windows WNS notification to tag {Tag}", tagExpression);
            }

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
