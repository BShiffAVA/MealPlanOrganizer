using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MealPlanOrganizer.Functions.Services;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

/// <summary>
/// Test implementation of IJwtValidationService that validates tokens
/// using the TestAuthHandler's signing key.
/// </summary>
public class TestJwtValidationService : IJwtValidationService
{
    private static readonly string TestSigningKey = "ThisIsATestSigningKeyForIntegrationTestsThatIsLongEnough256Bits!";
    
    private static readonly SymmetricSecurityKey SigningKey = 
        new(Encoding.UTF8.GetBytes(TestSigningKey));
    
    private readonly TokenValidationParameters _validationParameters;
    
    public TestJwtValidationService()
    {
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = TestAuthHandler.TestIssuer,
            ValidAudience = TestAuthHandler.TestAudience,
            IssuerSigningKey = SigningKey,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }
    
    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, _validationParameters, out var validatedToken);
            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch (SecurityTokenException)
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }
    
    public string? GetUserId(ClaimsPrincipal principal)
    {
        // Check for Azure AD object ID claim first (oid)
        var oid = principal.FindFirst("oid")?.Value;
        if (!string.IsNullOrEmpty(oid)) return oid;
        
        // Fall back to sub claim
        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        return sub;
    }
    
    public string? GetUserEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value 
            ?? principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? principal.FindFirst("emails")?.Value;
    }
    
    public string? GetUserDisplayName(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Name)?.Value 
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
    }
}
