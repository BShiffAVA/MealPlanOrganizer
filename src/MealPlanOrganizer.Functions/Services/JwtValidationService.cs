using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Service for validating JWT tokens from Microsoft Entra External ID.
/// </summary>
public class JwtValidationService : IJwtValidationService
{
    private readonly ILogger<JwtValidationService> _logger;
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly string _tenantId;
    private readonly string _clientId;

    public JwtValidationService(IConfiguration configuration, ILogger<JwtValidationService> logger)
    {
        _logger = logger;

        // Get configuration from settings
        // Support both double-underscore (env vars) and colon (JSON) notation
        _tenantId = configuration["AzureAd__TenantId"] 
            ?? configuration["AzureAd:TenantId"] 
            ?? throw new InvalidOperationException("AzureAd:TenantId is not configured");
        
        _clientId = configuration["AzureAd__ClientId"] 
            ?? configuration["AzureAd:ClientId"] 
            ?? throw new InvalidOperationException("AzureAd:ClientId is not configured");

        var authority = configuration["AzureAd__Authority"] 
            ?? configuration["AzureAd:Authority"];

        // For External ID (CIAM), the authority format is different
        // Format: https://{tenant-name}.ciamlogin.com/{tenant-id}/v2.0
        if (string.IsNullOrEmpty(authority))
        {
            // Try to construct from tenant name for External ID
            var tenantName = configuration["AzureAd__TenantName"] 
                ?? configuration["AzureAd:TenantName"];
            
            if (!string.IsNullOrEmpty(tenantName))
            {
                authority = $"https://{tenantName}.ciamlogin.com/{_tenantId}/v2.0";
            }
            else
            {
                // Fallback to standard Azure AD authority
                authority = $"https://login.microsoftonline.com/{_tenantId}/v2.0";
            }
        }

        _logger.LogInformation("Configuring JWT validation for authority: {Authority}", authority);

        // Set up OpenID Connect configuration manager for automatic key refresh
        var metadataAddress = $"{authority}/.well-known/openid-configuration";
        _logger.LogInformation("OIDC metadata address: {MetadataAddress}", metadataAddress);
        
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        // Build valid issuers list - handle both config notations
        var tenantNameForIssuers = configuration["AzureAd__TenantName"] ?? configuration["AzureAd:TenantName"];
        var validIssuers = new List<string> { authority };
        
        if (!string.IsNullOrEmpty(tenantNameForIssuers))
        {
            validIssuers.Add($"https://{tenantNameForIssuers}.ciamlogin.com/{_tenantId}/v2.0");
            // Also add the issuer format used by the CIAM OIDC config
            validIssuers.Add($"https://{_tenantId}.ciamlogin.com/{_tenantId}/v2.0");
        }
        validIssuers.Add($"https://login.microsoftonline.com/{_tenantId}/v2.0");
        // Add the sts.windows.net issuer format (used by some tokens)
        validIssuers.Add($"https://sts.windows.net/{_tenantId}/");
        
        _logger.LogInformation("Valid issuers configured: {Issuers}", string.Join(", ", validIssuers));

        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = _clientId,
            ValidIssuers = validIssuers,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Token is null or empty");
            return null;
        }

        try
        {
            // Decode token to see what's in it (without validation)
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            _logger.LogInformation("Token issuer: {Issuer}, audience: {Audience}, kid: {Kid}", 
                jwtToken.Issuer, 
                string.Join(",", jwtToken.Audiences), 
                jwtToken.Header.Kid);
            
            // Get the OpenID Connect configuration (includes signing keys)
            var config = await _configurationManager.GetConfigurationAsync(CancellationToken.None);
            
            _logger.LogInformation("OIDC config loaded. Issuer: {Issuer}, SigningKeys count: {KeyCount}, JwksUri: {JwksUri}", 
                config.Issuer, config.SigningKeys?.Count() ?? 0, config.JwksUri);
            
            // Log the key IDs we have
            if (config.SigningKeys != null)
            {
                var keyIds = config.SigningKeys.Select(k => k.KeyId).ToList();
                _logger.LogInformation("Available signing key IDs: {KeyIds}", string.Join(", ", keyIds));
            }
            
            var validationParameters = _tokenValidationParameters.Clone();
            validationParameters.IssuerSigningKeys = config.SigningKeys;
            
            // Also add the issuer from the configuration as valid
            var validIssuers = validationParameters.ValidIssuers?.ToList() ?? new List<string>();
            if (!string.IsNullOrEmpty(config.Issuer) && !validIssuers.Contains(config.Issuer))
            {
                validIssuers.Add(config.Issuer);
                validationParameters.ValidIssuers = validIssuers;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is JwtSecurityToken validatedJwt)
            {
                _logger.LogDebug("Token validated successfully. Subject: {Subject}", validatedJwt.Subject);
            }

            return principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning("Token has expired: {Message}", ex.Message);
            return null;
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning("Token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating token");
            return null;
        }
    }

    public string? GetUserId(ClaimsPrincipal principal)
    {
        // External ID uses 'oid' (object ID) as the unique user identifier
        return principal.FindFirst("oid")?.Value 
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
    }

    public string? GetUserEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirst("email")?.Value
            ?? principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;
    }

    public string? GetUserDisplayName(ClaimsPrincipal principal)
    {
        return principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("given_name")?.Value;
    }
}
