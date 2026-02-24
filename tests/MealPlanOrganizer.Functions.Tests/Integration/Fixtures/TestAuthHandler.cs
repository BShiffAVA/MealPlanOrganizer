using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

/// <summary>
/// Test authentication handler for generating JWT tokens in integration tests.
/// Provides utilities for creating valid and invalid tokens with various configurations.
/// </summary>
public static class TestAuthHandler
{
    // Test signing key - long enough for HS256 (256 bits = 32 bytes minimum)
    private static readonly string TestSigningKey = "ThisIsATestSigningKeyForIntegrationTestsThatIsLongEnough256Bits!";
    public static readonly string TestIssuer = "https://test.mealplanorganizer.com/";
    public static readonly string TestAudience = "mealplanorganizer-api";
    
    private static readonly SymmetricSecurityKey SigningKey = 
        new(Encoding.UTF8.GetBytes(TestSigningKey));
    
    private static readonly SigningCredentials SigningCredentials = 
        new(SigningKey, SecurityAlgorithms.HmacSha256);
    
    /// <summary>
    /// Creates a valid JWT token for the specified user.
    /// </summary>
    public static string CreateToken(
        string userId, 
        string? householdId = null, 
        string? displayName = null,
        string? email = null,
        IEnumerable<string>? roles = null,
        TimeSpan? expiration = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        
        if (!string.IsNullOrEmpty(householdId))
        {
            claims.Add(new Claim("household_id", householdId));
        }
        
        if (!string.IsNullOrEmpty(displayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, displayName));
            claims.Add(new Claim("name", displayName));
        }
        
        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        }
        
        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        
        var tokenExpiration = expiration ?? TimeSpan.FromHours(1);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(tokenExpiration),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = SigningCredentials
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Creates an Authorization header with a valid Bearer token.
    /// </summary>
    public static AuthenticationHeaderValue CreateAuthHeader(
        string userId, 
        string? householdId = null,
        string? displayName = null)
    {
        var token = CreateToken(userId, householdId, displayName);
        return new AuthenticationHeaderValue("Bearer", token);
    }
    
    /// <summary>
    /// Creates an expired JWT token.
    /// </summary>
    public static string CreateExpiredToken(string userId, string? householdId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Sub, userId)
        };
        
        if (!string.IsNullOrEmpty(householdId))
        {
            claims.Add(new Claim("household_id", householdId));
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(-1), // Already expired
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = SigningCredentials
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Creates an Authorization header with an expired token.
    /// </summary>
    public static AuthenticationHeaderValue CreateExpiredAuthHeader(string userId, string? householdId = null)
    {
        var token = CreateExpiredToken(userId, householdId);
        return new AuthenticationHeaderValue("Bearer", token);
    }
    
    /// <summary>
    /// Creates a token signed with the wrong key (invalid signature).
    /// </summary>
    public static string CreateInvalidSignatureToken(string userId)
    {
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("WrongSigningKeyForTestingInvalidTokenVerification!"));
        var wrongCredentials = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = wrongCredentials
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Creates an Authorization header with an invalid signature token.
    /// </summary>
    public static AuthenticationHeaderValue CreateInvalidSignatureAuthHeader(string userId)
    {
        var token = CreateInvalidSignatureToken(userId);
        return new AuthenticationHeaderValue("Bearer", token);
    }
    
    /// <summary>
    /// Creates a malformed token string.
    /// </summary>
    public static string CreateMalformedToken()
    {
        return "not.a.valid.jwt.token";
    }
    
    /// <summary>
    /// Creates an Authorization header with a malformed token.
    /// </summary>
    public static AuthenticationHeaderValue CreateMalformedAuthHeader()
    {
        return new AuthenticationHeaderValue("Bearer", CreateMalformedToken());
    }
    
    /// <summary>
    /// Creates a token with wrong issuer.
    /// </summary>
    public static string CreateWrongIssuerToken(string userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "https://wrong.issuer.com/",
            Audience = TestAudience,
            SigningCredentials = SigningCredentials
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Creates a token with wrong audience.
    /// </summary>
    public static string CreateWrongAudienceToken(string userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = TestIssuer,
            Audience = "wrong-audience",
            SigningCredentials = SigningCredentials
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Gets the TokenValidationParameters configured for test tokens.
    /// Use this to configure test server authentication.
    /// </summary>
    public static TokenValidationParameters GetTestTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
    
    /// <summary>
    /// Gets test token validation parameters with relaxed validation for specific scenarios.
    /// </summary>
    public static TokenValidationParameters GetRelaxedTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }
}

/// <summary>
/// Pre-configured test users for integration tests.
/// </summary>
public static class TestUsers
{
    public static readonly TestUser User1 = new()
    {
        UserId = TestData.User1Id.ToString(),
        HouseholdId = TestData.HouseholdId.ToString(),
        DisplayName = "Test User 1",
        Email = "user1@test.com"
    };
    
    public static readonly TestUser User2 = new()
    {
        UserId = TestData.User2Id.ToString(),
        HouseholdId = TestData.HouseholdId.ToString(),
        DisplayName = "Test User 2",
        Email = "user2@test.com"
    };
    
    public static readonly TestUser AnotherHouseholdUser = new()
    {
        UserId = Guid.NewGuid().ToString(),
        HouseholdId = Guid.NewGuid().ToString(),
        DisplayName = "Other Household User",
        Email = "other@household.com"
    };
    
    public static readonly TestUser AdminUser = new()
    {
        UserId = Guid.NewGuid().ToString(),
        HouseholdId = TestData.HouseholdId.ToString(),
        DisplayName = "Admin User",
        Email = "admin@test.com",
        Roles = new[] { "Admin" }
    };
    
    public static readonly TestUser NonHouseholdUser = new()
    {
        UserId = Guid.NewGuid().ToString(),
        HouseholdId = Guid.NewGuid().ToString(),
        DisplayName = "No Household User",
        Email = "nohousehold@test.com"
    };
}

/// <summary>
/// Represents a test user configuration.
/// </summary>
public class TestUser
{
    public required string UserId { get; init; }
    public required string HouseholdId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Creates a JWT token for this test user.
    /// </summary>
    public string CreateToken(TimeSpan? expiration = null)
    {
        return TestAuthHandler.CreateToken(
            UserId, 
            HouseholdId, 
            DisplayName, 
            Email, 
            Roles,
            expiration);
    }
    
    /// <summary>
    /// Creates an Authorization header for this test user.
    /// </summary>
    public AuthenticationHeaderValue CreateAuthHeader()
    {
        return TestAuthHandler.CreateAuthHeader(UserId, HouseholdId, DisplayName);
    }
}
