using System.Security.Claims;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Service interface for validating JWT tokens from Microsoft Entra External ID.
/// </summary>
public interface IJwtValidationService
{
    /// <summary>
    /// Validates a JWT token and returns the claims principal if valid.
    /// </summary>
    /// <param name="token">The JWT token to validate (without "Bearer " prefix).</param>
    /// <returns>The ClaimsPrincipal if valid, null otherwise.</returns>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

    /// <summary>
    /// Extracts the user ID from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal from a validated token.</param>
    /// <returns>The user's object ID (oid claim), or null if not found.</returns>
    string? GetUserId(ClaimsPrincipal principal);

    /// <summary>
    /// Extracts the user's email from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal from a validated token.</param>
    /// <returns>The user's email, or null if not found.</returns>
    string? GetUserEmail(ClaimsPrincipal principal);

    /// <summary>
    /// Extracts the user's display name from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal from a validated token.</param>
    /// <returns>The user's display name, or null if not found.</returns>
    string? GetUserDisplayName(ClaimsPrincipal principal);
}
