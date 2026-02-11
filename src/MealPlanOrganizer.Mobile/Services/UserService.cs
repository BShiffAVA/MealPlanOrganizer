using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MealPlanOrganizer.Mobile.Models;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service for user management operations.
/// </summary>
public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<UserService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionKey;
    private readonly JsonSerializerOptions _jsonOptions;

    // Cache the user to avoid repeated API calls
    private UserDto? _cachedUser;

    public UserService(
        HttpClient httpClient, 
        IAuthService authService, 
        ILogger<UserService> logger, 
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        _baseUrl = configuration["AzureFunctions:BaseUrl"] ?? throw new InvalidOperationException("AzureFunctions:BaseUrl not configured");
        _functionKey = configuration["AzureFunctions:FunctionKey"] ?? throw new InvalidOperationException("AzureFunctions:FunctionKey not configured");
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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

    /// <inheritdoc/>
    public async Task<UserDto?> RegisterUserAsync()
    {
        try
        {
            _logger.LogInformation("Registering user with backend");

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/users/register?code={_functionKey}";
            var response = await _httpClient.PostAsync(url, null);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to register user: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<UserDto>(jsonContent, _jsonOptions);
            
            _cachedUser = user;
            _logger.LogInformation("Successfully registered user: {UserId}", user?.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception registering user");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            _logger.LogInformation("Getting current user from backend");

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/users/me?code={_functionKey}";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("User not found - needs registration");
                _cachedUser = null;
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get current user: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<UserDto>(jsonContent, _jsonOptions);
            
            _cachedUser = user;
            _logger.LogInformation("Successfully got current user: {UserId}, HasHousehold: {HasHousehold}", 
                user?.Id, user?.Household != null);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting current user");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<HouseholdDto?> CreateHouseholdAsync(string name)
    {
        try
        {
            _logger.LogInformation("Creating household: {Name}", name);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households?code={_functionKey}";
            var requestBody = new { name };
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody), 
                Encoding.UTF8, 
                "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create household: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var household = JsonSerializer.Deserialize<HouseholdDto>(jsonContent, _jsonOptions);
            
            // Invalidate cached user since household changed
            _cachedUser = null;
            
            _logger.LogInformation("Successfully created household: {HouseholdId}", household?.Id);
            return household;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating household");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasHouseholdAsync()
    {
        if (_cachedUser != null)
        {
            return _cachedUser.Household != null;
        }
        
        var user = await GetCurrentUserAsync();
        return user?.Household != null;
    }
}
