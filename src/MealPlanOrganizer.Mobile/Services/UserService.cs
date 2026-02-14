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

    /// <inheritdoc/>
    public async Task<ValidateInviteCodeResponse?> ValidateInviteCodeAsync(string code)
    {
        try
        {
            _logger.LogInformation("Validating invite code");

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/invites/{Uri.EscapeDataString(code)}/validate?code={_functionKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to validate invite code: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return new ValidateInviteCodeResponse
                {
                    IsValid = false,
                    ErrorMessage = "Failed to validate invite code"
                };
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ValidateInviteCodeResponse>(jsonContent, _jsonOptions);
            
            _logger.LogInformation("Invite code validation result: IsValid={IsValid}", result?.IsValid);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception validating invite code");
            return new ValidateInviteCodeResponse
            {
                IsValid = false,
                ErrorMessage = "An error occurred while validating the code"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<JoinHouseholdResponse?> JoinHouseholdAsync(string code)
    {
        try
        {
            _logger.LogInformation("Joining household with invite code");

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/join?code={_functionKey}";
            var requestBody = new { inviteCode = code };
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody), 
                Encoding.UTF8, 
                "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to join household: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JoinHouseholdResponse>(jsonContent, _jsonOptions);
            
            // Invalidate cached user since household changed
            _cachedUser = null;
            
            _logger.LogInformation("Successfully joined household: {HouseholdId}", result?.HouseholdId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception joining household");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<InviteCodeDto?> GenerateInviteCodeAsync(Guid householdId)
    {
        try
        {
            _logger.LogInformation("Generating invite code for household {HouseholdId}", householdId);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/{householdId}/invites?code={_functionKey}";
            var response = await _httpClient.PostAsync(url, null);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to generate invite code: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<InviteCodeDto>(jsonContent, _jsonOptions);
            
            _logger.LogInformation("Successfully generated invite code: {Code}", result?.Code);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception generating invite code");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<List<InviteCodeDto>> GetInviteCodesAsync(Guid householdId, bool includeUsed = false)
    {
        try
        {
            _logger.LogInformation("Getting invite codes for household {HouseholdId}", householdId);

            await AttachBearerTokenAsync();

            var queryParams = includeUsed ? "includeUsed=true" : "";
            var url = $"{_baseUrl}/households/{householdId}/invites?code={_functionKey}&{queryParams}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get invite codes: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return new List<InviteCodeDto>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<InviteCodeDto>>(jsonContent, _jsonOptions);
            
            _logger.LogInformation("Retrieved {Count} invite codes", result?.Count ?? 0);
            return result ?? new List<InviteCodeDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting invite codes");
            return new List<InviteCodeDto>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeInviteCodeAsync(string code)
    {
        try
        {
            _logger.LogInformation("Revoking invite code {Code}", code);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/invites/{Uri.EscapeDataString(code)}?code={_functionKey}";
            var response = await _httpClient.DeleteAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to revoke invite code: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Successfully revoked invite code: {Code}", code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception revoking invite code");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId)
    {
        try
        {
            _logger.LogInformation("Removing member {MemberId} from household {HouseholdId}", memberId, householdId);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/{householdId}/members/{memberId}?code={_functionKey}";
            var response = await _httpClient.DeleteAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to remove member: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Successfully removed member {MemberId} from household {HouseholdId}", memberId, householdId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception removing member");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<HouseholdMemberDto?> UpdateMemberWeightAsync(Guid householdId, Guid memberId, int weight)
    {
        try
        {
            _logger.LogInformation("Updating weight for member {MemberId} to {Weight} in household {HouseholdId}", memberId, weight, householdId);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/{householdId}/members/{memberId}/weight?code={_functionKey}";
            var content = new StringContent(
                JsonSerializer.Serialize(new { weight }, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = content
            };
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update member weight: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var memberDto = JsonSerializer.Deserialize<HouseholdMemberDto>(responseBody, _jsonOptions);

            _logger.LogInformation("Successfully updated weight for member {MemberId} to {Weight}", memberId, weight);
            return memberDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception updating member weight");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<HouseholdDto?> UpdateHouseholdAsync(Guid householdId, string? name = null, string? timeZoneId = null)
    {
        try
        {
            _logger.LogInformation("Updating household {HouseholdId}: Name={Name}, TimeZoneId={TimeZoneId}", 
                householdId, name, timeZoneId);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/households/{householdId}?code={_functionKey}";
            var requestBody = new Dictionary<string, string?>();
            
            if (!string.IsNullOrWhiteSpace(name))
            {
                requestBody["name"] = name;
            }
            
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                requestBody["timeZoneId"] = timeZoneId;
            }

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = content
            };
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update household: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var householdDto = JsonSerializer.Deserialize<HouseholdDto>(responseBody, _jsonOptions);

            // Invalidate user cache so next GetCurrentUserAsync() fetches fresh data
            _cachedUser = null;

            _logger.LogInformation("Successfully updated household {HouseholdId}", householdId);
            return householdDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception updating household");
            return null;
        }
    }

    // Cache timezones since they rarely change
    private List<string>? _cachedTimezones;

    /// <inheritdoc/>
    public async Task<List<string>> GetTimezonesAsync()
    {
        if (_cachedTimezones != null)
        {
            return _cachedTimezones;
        }

        try
        {
            _logger.LogInformation("Fetching available timezones");

            var url = $"{_baseUrl}/timezones?code={_functionKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch timezones: {StatusCode}", response.StatusCode);
                return GetFallbackTimezones();
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _cachedTimezones = JsonSerializer.Deserialize<List<string>>(responseBody, _jsonOptions) ?? GetFallbackTimezones();
            
            return _cachedTimezones;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching timezones");
            return GetFallbackTimezones();
        }
    }

    private static List<string> GetFallbackTimezones() => new()
    {
        "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles", "UTC"
    };
}
