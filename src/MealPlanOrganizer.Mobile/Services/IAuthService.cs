using Microsoft.Identity.Client;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service interface for authentication operations using Microsoft Entra External ID.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets whether the user is currently authenticated (has valid cached tokens).
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Initiates interactive login flow with Microsoft Entra External ID.
    /// </summary>
    /// <returns>Authentication result with tokens, or null if cancelled/failed.</returns>
    Task<AuthenticationResult?> LoginAsync();

    /// <summary>
    /// Attempts to acquire a token silently from cache.
    /// Falls back to interactive login if silent acquisition fails.
    /// </summary>
    /// <returns>Authentication result with tokens, or null if failed.</returns>
    Task<AuthenticationResult?> GetAccessTokenSilentlyAsync();

    /// <summary>
    /// Gets the current access token for API calls.
    /// Attempts silent acquisition first, then interactive if needed.
    /// </summary>
    /// <returns>Access token string, or null if not authenticated.</returns>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// Logs out the current user and clears all cached tokens.
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Gets the current user's account information.
    /// </summary>
    Task<IAccount?> GetCurrentAccountAsync();

    /// <summary>
    /// Gets the current user's display name from cached account.
    /// </summary>
    Task<string?> GetUserDisplayNameAsync();

    /// <summary>
    /// Gets the current user's email from cached account.
    /// </summary>
    Task<string?> GetUserEmailAsync();
}
