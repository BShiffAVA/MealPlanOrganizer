using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Cross-platform push notification service that handles device registration 
/// with the backend API. Platform-specific token acquisition is handled in 
/// partial classes under the Platforms folder.
/// </summary>
public partial class PushNotificationService : IPushNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionKey;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private const string EnabledPreferenceKey = "PushNotificationsEnabled";
    private const string PushTokenPreferenceKey = "PushToken";
    
    private string? _currentPushToken;

    public PushNotificationService(
        HttpClient httpClient,
        IAuthService authService,
        ILogger<PushNotificationService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        _baseUrl = configuration["AzureFunctions:BaseUrl"] ?? throw new InvalidOperationException("AzureFunctions:BaseUrl not configured");
        _functionKey = configuration["AzureFunctions:FunctionKey"] ?? throw new InvalidOperationException("AzureFunctions:FunctionKey not configured");
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get => Preferences.Get(EnabledPreferenceKey, true);
        set => Preferences.Set(EnabledPreferenceKey, value);
    }

    /// <inheritdoc/>
    public string? CurrentPushToken
    {
        get => _currentPushToken ?? Preferences.Get(PushTokenPreferenceKey, null as string);
        private set
        {
            _currentPushToken = value;
            if (value != null)
            {
                Preferences.Set(PushTokenPreferenceKey, value);
            }
            else
            {
                Preferences.Remove(PushTokenPreferenceKey);
            }
        }
    }

    /// <inheritdoc/>
    public string Platform =>
#if ANDROID
        "android";
#elif IOS
        "ios";
#elif WINDOWS
        "windows";
#elif MACCATALYST
        "macos";
#else
        "unknown";
#endif

    /// <inheritdoc/>
    public bool IsSupported =>
#if ANDROID || IOS || WINDOWS
        true;
#else
        false;
#endif

    /// <inheritdoc/>
    public event EventHandler<PushNotificationReceivedEventArgs>? NotificationReceived;

    /// <summary>
    /// Raises the NotificationReceived event.
    /// </summary>
    protected void OnNotificationReceived(PushNotificationReceivedEventArgs args)
    {
        NotificationReceived?.Invoke(this, args);
    }

    /// <inheritdoc/>
    public async Task<bool> RegisterDeviceAsync()
    {
        if (!IsSupported)
        {
            _logger.LogWarning("Push notifications not supported on this platform");
            return false;
        }

        if (!IsEnabled)
        {
            _logger.LogInformation("Push notifications disabled by user");
            return false;
        }

        var token = CurrentPushToken;
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No push token available - initialize first");
            return false;
        }

        try
        {
            _logger.LogInformation("Registering device for push notifications, platform: {Platform}", Platform);

            await AttachBearerTokenAsync();

            var requestBody = JsonSerializer.Serialize(new
            {
                platform = Platform,
                pushToken = token
            });

            var url = $"{_baseUrl}/devices/register?code={_functionKey}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to register device: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Device registered successfully for push notifications");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception registering device for push notifications");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UnregisterDeviceAsync()
    {
        var token = CurrentPushToken;
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("No push token to unregister");
            return true;
        }

        try
        {
            _logger.LogInformation("Unregistering device from push notifications");

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/devices/unregister?code={_functionKey}&platform={Platform}&pushToken={Uri.EscapeDataString(token)}";
            var response = await _httpClient.DeleteAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to unregister device: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                return false;
            }

            // Clear local token
            CurrentPushToken = null;
            _logger.LogInformation("Device unregistered from push notifications");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception unregistering device from push notifications");
            return false;
        }
    }

    /// <summary>
    /// Sets the push token obtained from platform-specific code.
    /// </summary>
    protected void SetPushToken(string token)
    {
        CurrentPushToken = token;
        _logger.LogInformation("Push token updated for platform {Platform}", Platform);
    }

    private async Task AttachBearerTokenAsync()
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _logger.LogDebug("Bearer token attached to request");
            }
            else
            {
                _logger.LogWarning("No access token available - request will be unauthenticated");
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach Bearer token");
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    // Platform-specific InitializeAsync is implemented in partial classes
}
