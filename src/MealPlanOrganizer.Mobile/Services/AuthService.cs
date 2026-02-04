using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Authentication service implementation using MSAL for Microsoft Entra External ID.
/// Handles login, logout, token acquisition, and caching.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IPublicClientApplication _msalClient;
    private readonly ILogger<AuthService> _logger;
    private readonly string[] _scopes;

    // Cache the current account to avoid repeated lookups
    private IAccount? _cachedAccount;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
    {
        _logger = logger;

        var tenantId = configuration["AzureAd:TenantId"] 
            ?? throw new InvalidOperationException("AzureAd:TenantId not configured");
        var clientId = configuration["AzureAd:ClientId"] 
            ?? throw new InvalidOperationException("AzureAd:ClientId not configured");
        var authority = configuration["AzureAd:Authority"] 
            ?? throw new InvalidOperationException("AzureAd:Authority not configured");

        // Scopes for External ID - openid and offline_access for token refresh
        _scopes = ["openid", "offline_access", "profile", "email"];

        // Build the MSAL public client application
        var builder = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"{authority}/{tenantId}")
            .WithLogging(MsalLogCallback, Microsoft.Identity.Client.LogLevel.Warning, enablePiiLogging: false);

#if ANDROID
        // Android-specific configuration - custom URI scheme for browser redirect
        builder = builder
            .WithParentActivityOrWindow(() => Platform.CurrentActivity)
            .WithRedirectUri($"msal{clientId}://auth");
#elif IOS
        // iOS-specific configuration - custom URI scheme for browser redirect
        builder = builder
            .WithIosKeychainSecurityGroup("com.companyname.mealplanorganizer.mobile")
            .WithRedirectUri($"msal{clientId}://auth");
#elif WINDOWS
        // Windows-specific configuration - use loopback redirect URI for system browser
        // MSAL on Windows only supports loopback URIs, not custom schemes
        builder = builder.WithRedirectUri("http://localhost");
#else
        // Default fallback
        builder = builder.WithRedirectUri($"msal{clientId}://auth");
#endif

        _msalClient = builder.Build();

        _logger.LogInformation("AuthService initialized with authority: {Authority}", authority);
    }

    private void MsalLogCallback(Microsoft.Identity.Client.LogLevel level, string message, bool containsPii)
    {
        var logLevel = level switch
        {
            Microsoft.Identity.Client.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            Microsoft.Identity.Client.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            Microsoft.Identity.Client.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            Microsoft.Identity.Client.LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Debug,
            _ => Microsoft.Extensions.Logging.LogLevel.Trace
        };

        _logger.Log(logLevel, "MSAL: {Message}", message);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            
            if (account == null)
            {
                _logger.LogDebug("No cached account found");
                return false;
            }

            // Try to acquire token silently to verify the session is still valid
            var result = await _msalClient
                .AcquireTokenSilent(_scopes, account)
                .ExecuteAsync();

            _cachedAccount = account;
            _logger.LogDebug("User is authenticated: {Username}", account.Username);
            return true;
        }
        catch (MsalUiRequiredException)
        {
            _logger.LogDebug("Token expired or requires interactive login");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<AuthenticationResult?> LoginAsync()
    {
        try
        {
            _logger.LogInformation("Starting interactive login");

            var result = await _msalClient
                .AcquireTokenInteractive(_scopes)
#if ANDROID
                .WithParentActivityOrWindow(Platform.CurrentActivity)
#endif
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();

            _cachedAccount = result.Account;
            _logger.LogInformation("Login successful for user: {Username}", result.Account.Username);
            
            return result;
        }
        catch (MsalClientException ex) when (ex.ErrorCode == "authentication_canceled")
        {
            _logger.LogInformation("Login was cancelled by user");
            return null;
        }
        catch (MsalServiceException ex)
        {
            _logger.LogError(ex, "MSAL service error during login: {ErrorCode}", ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            throw;
        }
    }

    public async Task<AuthenticationResult?> GetAccessTokenSilentlyAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                _logger.LogDebug("No cached account for silent token acquisition");
                return null;
            }

            var result = await _msalClient
                .AcquireTokenSilent(_scopes, account)
                .ExecuteAsync();

            _cachedAccount = account;
            _logger.LogDebug("Silent token acquisition successful");
            
            return result;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogDebug("Silent token acquisition failed, UI required: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during silent token acquisition");
            return null;
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        // First try silent acquisition
        var result = await GetAccessTokenSilentlyAsync();
        
        if (result != null)
        {
            return result.AccessToken;
        }

        // If silent fails, try interactive (only if we have a UI context)
        _logger.LogDebug("Silent acquisition failed, attempting interactive login");
        result = await LoginAsync();
        
        return result?.AccessToken;
    }

    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Logging out user");

            var accounts = await _msalClient.GetAccountsAsync();
            
            foreach (var account in accounts)
            {
                await _msalClient.RemoveAsync(account);
                _logger.LogDebug("Removed account: {Username}", account.Username);
            }

            _cachedAccount = null;
            _logger.LogInformation("Logout completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    public async Task<IAccount?> GetCurrentAccountAsync()
    {
        if (_cachedAccount != null)
        {
            return _cachedAccount;
        }

        var accounts = await _msalClient.GetAccountsAsync();
        _cachedAccount = accounts.FirstOrDefault();
        
        return _cachedAccount;
    }

    public async Task<string?> GetUserDisplayNameAsync()
    {
        var account = await GetCurrentAccountAsync();
        
        // The display name may be in the username or we can extract from claims
        // For External ID, the username is typically the email
        return account?.Username;
    }

    public async Task<string?> GetUserEmailAsync()
    {
        var account = await GetCurrentAccountAsync();
        return account?.Username;
    }
}
