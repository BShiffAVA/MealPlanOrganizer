using FluentAssertions;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Unit.Services;

/// <summary>
/// Unit tests for JwtValidationService.
/// 
/// Note: Tests involving actual token validation require OIDC configuration
/// and are categorized as integration tests. Unit tests focus on configuration
/// validation and claim extraction logic.
/// </summary>
public class JwtValidationServiceTests
{
    private readonly Mock<ILogger<JwtValidationService>> _loggerMock;
    
    public JwtValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<JwtValidationService>>();
    }

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithMissingTenantId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AzureAd__TenantId"]).Returns((string?)null);
        configMock.Setup(c => c["AzureAd:TenantId"]).Returns((string?)null);
        configMock.Setup(c => c["AzureAd__ClientId"]).Returns("test-client-id");
        configMock.Setup(c => c["AzureAd:ClientId"]).Returns("test-client-id");

        // Act & Assert
        var act = () => new JwtValidationService(configMock.Object, _loggerMock.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TenantId*");
    }

    [Fact]
    public void Constructor_WithMissingClientId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AzureAd__TenantId"]).Returns("test-tenant-id");
        configMock.Setup(c => c["AzureAd:TenantId"]).Returns("test-tenant-id");
        configMock.Setup(c => c["AzureAd__ClientId"]).Returns((string?)null);
        configMock.Setup(c => c["AzureAd:ClientId"]).Returns((string?)null);

        // Act & Assert
        var act = () => new JwtValidationService(configMock.Object, _loggerMock.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ClientId*");
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesService()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var service = new JwtValidationService(config, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTenantName_UsesCiamAuthority()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AzureAd__TenantId"]).Returns("test-tenant-guid");
        configMock.Setup(c => c["AzureAd__ClientId"]).Returns("test-client-id");
        configMock.Setup(c => c["AzureAd__TenantName"]).Returns("mycompany");
        configMock.Setup(c => c["AzureAd__Authority"]).Returns((string?)null);
        configMock.Setup(c => c["AzureAd:Authority"]).Returns((string?)null);

        // Act
        var service = new JwtValidationService(configMock.Object, _loggerMock.Object);

        // Assert - should not throw; CIAM authority is constructed
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithExplicitAuthority_UsesProvidedAuthority()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AzureAd__TenantId"]).Returns("test-tenant-guid");
        configMock.Setup(c => c["AzureAd__ClientId"]).Returns("test-client-id");
        configMock.Setup(c => c["AzureAd__Authority"]).Returns("https://custom-authority.com/v2.0");

        // Act
        var service = new JwtValidationService(configMock.Object, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithColonNotation_ReadsConfigCorrectly()
    {
        // Arrange - using colon notation like JSON config files
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AzureAd__TenantId"]).Returns((string?)null);
        configMock.Setup(c => c["AzureAd:TenantId"]).Returns("test-tenant-id");
        configMock.Setup(c => c["AzureAd__ClientId"]).Returns((string?)null);
        configMock.Setup(c => c["AzureAd:ClientId"]).Returns("test-client-id");

        // Act
        var service = new JwtValidationService(configMock.Object, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region ValidateTokenAsync Tests

    [Fact]
    public async Task ValidateTokenAsync_WithNullToken_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);

        // Act
        var result = await service.ValidateTokenAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithEmptyToken_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);

        // Act
        var result = await service.ValidateTokenAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithWhitespaceToken_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);

        // Act
        var result = await service.ValidateTokenAsync("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithMalformedToken_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);

        // Act
        var result = await service.ValidateTokenAsync("not-a-valid-jwt-token");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithExpiredTokenFormat_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        
        // Create a fake expired JWT (this token format is recognizable but invalid)
        var expiredToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZXhwIjoxMDAwMDAwMDAwfQ.invalid-signature";

        // Act
        var result = await service.ValidateTokenAsync(expiredToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetUserId Tests

    [Fact]
    public void GetUserId_WithOidClaim_ReturnsOidValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim("oid", "user-object-id-123") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var userId = service.GetUserId(principal);

        // Assert
        userId.Should().Be("user-object-id-123");
    }

    [Fact]
    public void GetUserId_WithNameIdentifierClaim_ReturnsNameIdentifierValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "name-identifier-123") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var userId = service.GetUserId(principal);

        // Assert
        userId.Should().Be("name-identifier-123");
    }

    [Fact]
    public void GetUserId_WithSubClaim_ReturnsSubValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim("sub", "subject-id-123") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var userId = service.GetUserId(principal);

        // Assert
        userId.Should().Be("subject-id-123");
    }

    [Fact]
    public void GetUserId_WithNoClaims_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var userId = service.GetUserId(principal);

        // Assert
        userId.Should().BeNull();
    }

    [Fact]
    public void GetUserId_WithMultipleFallbackClaims_PrefsersOid()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim>
        {
            new Claim("oid", "oid-value"),
            new Claim(ClaimTypes.NameIdentifier, "name-id-value"),
            new Claim("sub", "sub-value")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var userId = service.GetUserId(principal);

        // Assert
        userId.Should().Be("oid-value");
    }

    #endregion

    #region GetUserEmail Tests

    [Fact]
    public void GetUserEmail_WithEmailClaim_ReturnsEmailValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim("email", "user@example.com") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var email = service.GetUserEmail(principal);

        // Assert
        email.Should().Be("user@example.com");
    }

    [Fact]
    public void GetUserEmail_WithPreferredUsernameClaim_ReturnsValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim("preferred_username", "preferred@example.com") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var email = service.GetUserEmail(principal);

        // Assert
        email.Should().Be("preferred@example.com");
    }

    [Fact]
    public void GetUserEmail_WithClaimTypesEmail_ReturnsValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim(ClaimTypes.Email, "claimtype@example.com") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var email = service.GetUserEmail(principal);

        // Assert
        email.Should().Be("claimtype@example.com");
    }

    [Fact]
    public void GetUserEmail_WithNoClaims_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var email = service.GetUserEmail(principal);

        // Assert
        email.Should().BeNull();
    }

    [Fact]
    public void GetUserEmail_WithMultipleFallbackClaims_PrefersEmailClaim()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim>
        {
            new Claim("email", "email@example.com"),
            new Claim("preferred_username", "preferred@example.com"),
            new Claim(ClaimTypes.Email, "claimtype@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var email = service.GetUserEmail(principal);

        // Assert
        email.Should().Be("email@example.com");
    }

    #endregion

    #region GetUserDisplayName Tests

    [Fact]
    public void GetUserDisplayName_WithNameClaim_ReturnsNameValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim("name", "John Doe") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var displayName = service.GetUserDisplayName(principal);

        // Assert
        displayName.Should().Be("John Doe");
    }

    [Fact]
    public void GetUserDisplayName_WithClaimTypesName_ReturnsValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "Jane Smith") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var displayName = service.GetUserDisplayName(principal);

        // Assert
        displayName.Should().Be("Jane Smith");
    }

    [Fact]
    public void GetUserDisplayName_WithGivenNameClaim_ReturnsValue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim> { new Claim("given_name", "Bob") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var displayName = service.GetUserDisplayName(principal);

        // Assert
        displayName.Should().Be("Bob");
    }

    [Fact]
    public void GetUserDisplayName_WithNoClaims_ReturnsNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var displayName = service.GetUserDisplayName(principal);

        // Assert
        displayName.Should().BeNull();
    }

    [Fact]
    public void GetUserDisplayName_WithMultipleFallbackClaims_PrefersNameClaim()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var service = new JwtValidationService(config, _loggerMock.Object);
        var claims = new List<Claim>
        {
            new Claim("name", "Full Name"),
            new Claim(ClaimTypes.Name, "Claims Type Name"),
            new Claim("given_name", "Given Name")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var displayName = service.GetUserDisplayName(principal);

        // Assert
        displayName.Should().Be("Full Name");
    }

    #endregion

    #region Helper Methods

    private static IConfiguration CreateValidConfiguration()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AzureAd__TenantId"]).Returns("test-tenant-id");
        configMock.Setup(c => c["AzureAd__ClientId"]).Returns("test-client-id");
        configMock.Setup(c => c["AzureAd__TenantName"]).Returns("testorg");
        return configMock.Object;
    }

    #endregion
}
