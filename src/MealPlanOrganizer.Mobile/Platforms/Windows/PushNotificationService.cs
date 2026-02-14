using Microsoft.Extensions.Logging;
using Windows.Networking.PushNotifications;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Windows-specific implementation of push notification initialization.
/// Uses Windows Notification Services (WNS) for push notification delivery.
/// </summary>
public partial class PushNotificationService
{
    private PushNotificationChannel? _channel;

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
            _logger.LogInformation("Initializing Windows push notifications (WNS)");

            // Request a push notification channel from WNS
            _channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
            
            if (_channel != null)
            {
                var channelUri = _channel.Uri;
                _logger.LogInformation("WNS channel acquired successfully");
                _logger.LogDebug("WNS Channel URI: {ChannelUri}", channelUri);
                
                // Store the channel URI as our push token
                SetPushToken(channelUri);
                
                // Subscribe to channel events
                _channel.PushNotificationReceived += OnWnsPushNotificationReceived;
                
                _logger.LogInformation("Windows push notification initialization complete");
            }
            else
            {
                _logger.LogWarning("Failed to acquire WNS channel - channel is null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Windows push notifications");
        }
    }

    /// <summary>
    /// Handles incoming push notifications when the app is in the foreground.
    /// </summary>
    private void OnWnsPushNotificationReceived(PushNotificationChannel sender, Windows.Networking.PushNotifications.PushNotificationReceivedEventArgs args)
    {
        try
        {
            string? title = null;
            string? body = null;
            Dictionary<string, string>? data = null;

            switch (args.NotificationType)
            {
                case PushNotificationType.Toast:
                    if (args.ToastNotification?.Content != null)
                    {
                        var xml = args.ToastNotification.Content;
                        // Extract text from toast XML
                        var textNodes = xml.GetElementsByTagName("text");
                        if (textNodes.Count > 0)
                        {
                            title = textNodes[0].InnerText;
                        }
                        if (textNodes.Count > 1)
                        {
                            body = textNodes[1].InnerText;
                        }
                    }
                    break;

                case PushNotificationType.Raw:
                    if (args.RawNotification != null)
                    {
                        body = args.RawNotification.Content;
                        // Try to parse raw content as JSON for data payload
                        try
                        {
                            data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                        }
                        catch
                        {
                            // Not JSON, keep as body
                        }
                    }
                    break;

                case PushNotificationType.Badge:
                    _logger.LogDebug("Badge notification received");
                    return; // Don't raise event for badge updates

                case PushNotificationType.Tile:
                    _logger.LogDebug("Tile notification received");
                    return; // Don't raise event for tile updates
            }

            _logger.LogInformation("Push notification received: {Title}", title ?? "(no title)");

            // Raise the notification received event
            OnNotificationReceived(new Services.PushNotificationReceivedEventArgs
            {
                Title = title,
                Body = body,
                Data = data ?? new Dictionary<string, string>()
            });

            // Let the system show the notification (don't cancel it)
            // args.Cancel = true; // Uncomment to suppress system notification
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing push notification");
        }
    }

    /// <summary>
    /// Cleanup method to release WNS channel resources.
    /// </summary>
    private void CleanupWindowsChannel()
    {
        if (_channel != null)
        {
            try
            {
                _channel.PushNotificationReceived -= OnWnsPushNotificationReceived;
                _channel.Close();
                _channel = null;
                _logger.LogDebug("WNS channel closed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing WNS channel");
            }
        }
    }
}
