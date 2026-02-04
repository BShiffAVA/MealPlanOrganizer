using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Helper class for authenticating HTTP requests in Azure Functions.
/// </summary>
public class AuthenticationHelper
{
    private readonly IJwtValidationService _jwtValidationService;
    private readonly ILogger<AuthenticationHelper> _logger;

    public AuthenticationHelper(IJwtValidationService jwtValidationService, ILogger<AuthenticationHelper> logger)
    {
        _jwtValidationService = jwtValidationService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates an HTTP request by validating the Bearer token.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>Authentication result with claims principal if successful.</returns>
    public async Task<AuthenticationResult> AuthenticateAsync(HttpRequestData request)
    {
        // Extract the Authorization header
        if (!request.Headers.TryGetValues("Authorization", out var authHeaderValues))
        {
            _logger.LogDebug("No Authorization header present");
            return AuthenticationResult.NoToken();
        }

        var authHeader = authHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            _logger.LogDebug("Authorization header is empty");
            return AuthenticationResult.NoToken();
        }

        // Check for Bearer token
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Authorization header is not a Bearer token");
            return AuthenticationResult.InvalidToken("Invalid authorization scheme");
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Bearer token is empty");
            return AuthenticationResult.InvalidToken("Token is empty");
        }

        // Validate the token
        var principal = await _jwtValidationService.ValidateTokenAsync(token);
        if (principal == null)
        {
            _logger.LogWarning("Token validation failed");
            return AuthenticationResult.InvalidToken("Token validation failed");
        }

        var userId = _jwtValidationService.GetUserId(principal);
        var email = _jwtValidationService.GetUserEmail(principal);
        
        _logger.LogInformation("Request authenticated. UserId: {UserId}, Email: {Email}", userId, email);
        
        return AuthenticationResult.Success(principal, userId, email);
    }

    /// <summary>
    /// Gets the user ID from a claims principal.
    /// </summary>
    public string? GetUserId(ClaimsPrincipal principal) => _jwtValidationService.GetUserId(principal);

    /// <summary>
    /// Gets the user email from a claims principal.
    /// </summary>
    public string? GetUserEmail(ClaimsPrincipal principal) => _jwtValidationService.GetUserEmail(principal);

    /// <summary>
    /// Gets the user display name from a claims principal.
    /// </summary>
    public string? GetUserDisplayName(ClaimsPrincipal principal) => _jwtValidationService.GetUserDisplayName(principal);
}

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public class AuthenticationResult
{
    public bool IsAuthenticated { get; private set; }
    public bool HasToken { get; private set; }
    public ClaimsPrincipal? Principal { get; private set; }
    public string? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string? ErrorMessage { get; private set; }

    private AuthenticationResult() { }

    public static AuthenticationResult Success(ClaimsPrincipal principal, string? userId, string? email)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = true,
            HasToken = true,
            Principal = principal,
            UserId = userId,
            UserEmail = email
        };
    }

    public static AuthenticationResult NoToken()
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            HasToken = false,
            ErrorMessage = "No authorization token provided"
        };
    }

    public static AuthenticationResult InvalidToken(string errorMessage)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            HasToken = true,
            ErrorMessage = errorMessage
        };
    }
}
