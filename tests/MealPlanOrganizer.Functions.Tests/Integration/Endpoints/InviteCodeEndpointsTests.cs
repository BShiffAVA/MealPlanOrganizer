using System.Net;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Functions;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using MealPlanOrganizer.Functions.Tests.Integration.Fixtures;
using MealPlanOrganizer.Functions.Tests.Integration.Helpers;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for Invite Code endpoints.
/// Tests GenerateInviteCode, GetInviteCodes, RevokeInviteCode, ValidateInviteCode, and JoinHousehold functions.
/// </summary>
[Collection("Integration")]
public class InviteCodeEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    
    public InviteCodeEndpointsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    public async Task InitializeAsync()
    {
        await _fixture.TestHost.ResetDatabaseAsync();
    }
    
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
    
    #region GenerateInviteCode Tests
    
    [Fact]
    public async Task GenerateInviteCode_AsAdmin_Returns201AndCreatesCode()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        // Create admin user with household
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new GenerateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var codeResponse = await ReadResponseBodyAsync<InviteCodeResponse>(response);
        Assert.NotNull(codeResponse);
        Assert.Equal(8, codeResponse.Code.Length);
        Assert.Equal(household.Id, codeResponse.HouseholdId);
        Assert.Equal(household.Name, codeResponse.HouseholdName);
        Assert.True(codeResponse.IsValid);
        Assert.False(codeResponse.IsUsed);
        Assert.False(codeResponse.IsRevoked);
        Assert.True(codeResponse.ExpiresUtc > DateTime.UtcNow);
        Assert.True(codeResponse.ExpiresUtc <= DateTime.UtcNow.AddDays(31));
        
        // Verify code was created in database
        var dbCode = await db.InviteCodes.FindAsync(codeResponse.Id);
        Assert.NotNull(dbCode);
        Assert.Equal(codeResponse.Code, dbCode.Code);
    }
    
    [Fact]
    public async Task GenerateInviteCode_AsNonAdmin_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        // Create admin user with household
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        // Create member user
        var memberExternalId = Guid.NewGuid().ToString();
        var memberUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = memberExternalId,
            Email = "member@example.com",
            DisplayName = "Member User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(memberUser);
        
        db.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = memberUser.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Member
        });
        await db.SaveChangesAsync();
        
        var memberToken = TestAuthHandler.CreateToken(userId: memberExternalId, email: "member@example.com");
        
        var function = new GenerateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: $"Bearer {memberToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task GenerateInviteCode_NonMember_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        // Create admin user with household
        var (_, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        // Create another user not in this household
        var otherExternalId = Guid.NewGuid().ToString();
        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = otherExternalId,
            Email = "other@example.com",
            DisplayName = "Other User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();
        
        var otherToken = TestAuthHandler.CreateToken(userId: otherExternalId, email: "other@example.com");
        
        var function = new GenerateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: $"Bearer {otherToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task GenerateInviteCode_InvalidHouseholdId_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, _, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new GenerateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid());
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GenerateInviteCode_NoAuth_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new GenerateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: null);
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid());
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    #endregion
    
    #region GetInviteCodes Tests
    
    [Fact]
    public async Task GetInviteCodes_AsAdmin_ReturnsActiveCodesOnly()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        // Create invite codes with different states
        var activeCode = new InviteCode
        {
            Code = "ACTIVE01",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30)
        };
        var revokedCode = new InviteCode
        {
            Code = "REVOKED1",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30),
            IsRevoked = true
        };
        var expiredCode = new InviteCode
        {
            Code = "EXPIRED1",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.InviteCodes.AddRange(activeCode, revokedCode, expiredCode);
        await db.SaveChangesAsync();
        
        var function = new GetInviteCodes(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var codes = await ReadResponseBodyAsync<List<InviteCodeResponse>>(response);
        Assert.NotNull(codes);
        Assert.Single(codes); // Only active code
        Assert.Equal("ACTIVE01", codes[0].Code);
    }
    
    [Fact]
    public async Task GetInviteCodes_AsNonAdmin_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        // Create member user
        var memberExternalId = Guid.NewGuid().ToString();
        var memberUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = memberExternalId,
            Email = "member2@example.com",
            DisplayName = "Member User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(memberUser);
        db.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = memberUser.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Member
        });
        await db.SaveChangesAsync();
        
        var memberToken = TestAuthHandler.CreateToken(userId: memberExternalId, email: "member2@example.com");
        
        var function = new GetInviteCodes(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {memberToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    #endregion
    
    #region RevokeInviteCode Tests
    
    [Fact]
    public async Task RevokeInviteCode_AsAdmin_Returns204()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "TODELETE",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30)
        };
        db.InviteCodes.Add(inviteCode);
        await db.SaveChangesAsync();
        
        var function = new RevokeInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, "TODELETE");
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify code is revoked in database
        await db.Entry(inviteCode).ReloadAsync();
        Assert.True(inviteCode.IsRevoked);
    }
    
    [Fact]
    public async Task RevokeInviteCode_AsNonAdmin_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "NOREVOKE",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30)
        };
        db.InviteCodes.Add(inviteCode);
        
        // Create member user
        var memberExternalId = Guid.NewGuid().ToString();
        var memberUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = memberExternalId,
            Email = "member3@example.com",
            DisplayName = "Member User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(memberUser);
        db.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = memberUser.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Member
        });
        await db.SaveChangesAsync();
        
        var memberToken = TestAuthHandler.CreateToken(userId: memberExternalId, email: "member3@example.com");
        
        var function = new RevokeInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {memberToken}");
        
        // Act
        var response = await function.Run(httpRequest, "NOREVOKE");
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task RevokeInviteCode_AlreadyRevoked_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "ALRDYREV",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30),
            IsRevoked = true
        };
        db.InviteCodes.Add(inviteCode);
        await db.SaveChangesAsync();
        
        var function = new RevokeInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, "ALRDYREV");
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task RevokeInviteCode_CodeNotFound_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, _, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new RevokeInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, "NOTFOUND");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    #endregion
    
    #region ValidateInviteCode Tests
    
    [Fact]
    public async Task ValidateInviteCode_ValidCode_ReturnsValid()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "VALIDCOD",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30)
        };
        db.InviteCodes.Add(inviteCode);
        
        // Create a new user to validate the code
        var newUserExternalId = Guid.NewGuid().ToString();
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "newuser@example.com",
            DisplayName = "New User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "newuser@example.com");
        
        var function = new ValidateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {newUserToken}");
        
        // Act
        var response = await function.Run(httpRequest, "VALIDCOD");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await ReadResponseBodyAsync<ValidateInviteCodeResponse>(response);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(household.Name, result.HouseholdName);
        Assert.Null(result.ErrorMessage);
    }
    
    [Fact]
    public async Task ValidateInviteCode_ExpiredCode_ReturnsInvalid()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "EXPIRED2",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.InviteCodes.Add(inviteCode);
        
        var newUserExternalId = Guid.NewGuid().ToString();
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "user2@example.com"
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "user2@example.com");
        
        var function = new ValidateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {newUserToken}");
        
        // Act
        var response = await function.Run(httpRequest, "EXPIRED2");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await ReadResponseBodyAsync<ValidateInviteCodeResponse>(response);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("expired", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }
    
    [Fact]
    public async Task ValidateInviteCode_RevokedCode_ReturnsInvalid()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "REVOKD01",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30),
            IsRevoked = true
        };
        db.InviteCodes.Add(inviteCode);
        
        var newUserExternalId = Guid.NewGuid().ToString();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "user3@example.com"
        });
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "user3@example.com");
        
        var function = new ValidateInviteCode(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {newUserToken}");
        
        // Act
        var response = await function.Run(httpRequest, "REVOKD01");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await ReadResponseBodyAsync<ValidateInviteCodeResponse>(response);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("revoked", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }
    
    #endregion
    
    #region JoinHousehold Tests
    
    [Fact]
    public async Task JoinHousehold_ValidCode_JoinsAndMarksCodeUsed()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "JOINCOD1",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30)
        };
        db.InviteCodes.Add(inviteCode);
        
        // Create a new user who will join
        var newUserExternalId = Guid.NewGuid().ToString();
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "joiner@example.com",
            DisplayName = "Joiner User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "joiner@example.com");
        
        var function = new JoinHousehold(loggerFactory, db, authHelper);
        var requestBody = new { inviteCode = "JOINCOD1" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post, 
            body: JsonSerializer.Serialize(requestBody),
            authHeader: $"Bearer {newUserToken}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify user is now a member
        var membership = db.HouseholdMembers.FirstOrDefault(hm => hm.UserId == newUser.Id);
        Assert.NotNull(membership);
        Assert.Equal(household.Id, membership.HouseholdId);
        Assert.Equal(HouseholdRole.Member, membership.Role);
        
        // Verify code is marked as used
        await db.Entry(inviteCode).ReloadAsync();
        Assert.Equal(newUser.Id, inviteCode.UsedByUserId);
        Assert.NotNull(inviteCode.UsedUtc);
    }
    
    [Fact]
    public async Task JoinHousehold_AlreadyMember_Returns409()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "ALRDYMEM",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30)
        };
        db.InviteCodes.Add(inviteCode);
        await db.SaveChangesAsync();
        
        var function = new JoinHousehold(loggerFactory, db, authHelper);
        var requestBody = new { inviteCode = "ALRDYMEM" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post, 
            body: JsonSerializer.Serialize(requestBody),
            authHeader: $"Bearer {adminToken}");
        
        // Act (Admin tries to join their own household)
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task JoinHousehold_UsedCode_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        // Create an already used invite code
        var previousUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = previousUserId,
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = "previous@example.com"
        });
        
        var inviteCode = new InviteCode
        {
            Code = "USEDCODE",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(30),
            UsedByUserId = previousUserId,
            UsedUtc = DateTime.UtcNow
        };
        db.InviteCodes.Add(inviteCode);
        
        // Create new user who tries to use the code
        var newUserExternalId = Guid.NewGuid().ToString();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "newjoiner@example.com"
        });
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "newjoiner@example.com");
        
        var function = new JoinHousehold(loggerFactory, db, authHelper);
        var requestBody = new { inviteCode = "USEDCODE" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post, 
            body: JsonSerializer.Serialize(requestBody),
            authHeader: $"Bearer {newUserToken}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task JoinHousehold_ExpiredCode_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        
        var inviteCode = new InviteCode
        {
            Code = "EXPIREDJ",
            HouseholdId = household.Id,
            CreatedByUserId = adminUser.Id,
            ExpiresUtc = DateTime.UtcNow.AddDays(-1) // Expired
        };
        db.InviteCodes.Add(inviteCode);
        
        var newUserExternalId = Guid.NewGuid().ToString();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "joinexp@example.com"
        });
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "joinexp@example.com");
        
        var function = new JoinHousehold(loggerFactory, db, authHelper);
        var requestBody = new { inviteCode = "EXPIREDJ" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post, 
            body: JsonSerializer.Serialize(requestBody),
            authHeader: $"Bearer {newUserToken}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    #endregion
    
    #region End-to-End Flow Tests
    
    [Fact]
    public async Task FullFlow_AdminGeneratesCode_MemberJoins()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        // Step 1: Admin creates household
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        // Step 2: Admin generates invite code
        var generateFunction = new GenerateInviteCode(loggerFactory, db, authHelper);
        var generateRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: $"Bearer {adminToken}");
        var generateResponse = await generateFunction.Run(generateRequest, household.Id);
        
        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
        var codeResponse = await ReadResponseBodyAsync<InviteCodeResponse>(generateResponse);
        Assert.NotNull(codeResponse);
        var inviteCode = codeResponse.Code;
        
        // Step 3: New user validates the code
        var newUserExternalId = Guid.NewGuid().ToString();
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = newUserExternalId,
            Email = "familymember@example.com",
            DisplayName = "Family Member",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();
        
        var newUserToken = TestAuthHandler.CreateToken(userId: newUserExternalId, email: "familymember@example.com");
        
        var validateFunction = new ValidateInviteCode(loggerFactory, db, authHelper);
        var validateRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {newUserToken}");
        var validateResponse = await validateFunction.Run(validateRequest, inviteCode);
        
        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);
        var validateResult = await ReadResponseBodyAsync<ValidateInviteCodeResponse>(validateResponse);
        Assert.True(validateResult?.IsValid);
        Assert.Equal(household.Name, validateResult?.HouseholdName);
        
        // Step 4: New user joins the household
        var joinFunction = new JoinHousehold(loggerFactory, db, authHelper);
        var joinBody = new { inviteCode = inviteCode };
        var joinRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(joinBody),
            authHeader: $"Bearer {newUserToken}");
        var joinResponse = await joinFunction.Run(joinRequest);
        
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        
        // Step 5: Verify user is now a member
        var membership = db.HouseholdMembers.FirstOrDefault(hm => hm.UserId == newUser.Id);
        Assert.NotNull(membership);
        Assert.Equal(HouseholdRole.Member, membership.Role);
        
        // Step 6: Admin lists invite codes - code should show as used
        var listFunction = new GetInviteCodes(loggerFactory, db, authHelper);
        var listRequest = CreateMockHttpRequest(
            HttpMethod.Get, 
            authHeader: $"Bearer {adminToken}",
            queryString: "includeUsed=true");
        var listResponse = await listFunction.Run(listRequest, household.Id);
        
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var codes = await ReadResponseBodyAsync<List<InviteCodeResponse>>(listResponse);
        var usedCode = codes?.FirstOrDefault(c => c.Code == inviteCode);
        Assert.NotNull(usedCode);
        Assert.True(usedCode.IsUsed);
    }
    
    #endregion
    
    #region Helper Methods
    
    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method,
        string? body = null,
        string? authHeader = null,
        string? queryString = null)
    {
        var url = queryString != null 
            ? $"http://localhost/api/test?{queryString}"
            : "http://localhost/api/test";
        return MockHttpFactory.CreateRequest(method, url, body, authHeader);
    }
    
    private static async Task<T?> ReadResponseBodyAsync<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }
    
    private async Task<(User adminUser, Household household, string token)> CreateAdminWithHouseholdAsync(AppDbContext db)
    {
        var externalId = Guid.NewGuid().ToString();
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = "admin@example.com",
            DisplayName = "Admin User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(adminUser);
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Test Household",
            CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = adminUser.Id
        };
        db.Households.Add(household);
        
        db.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = adminUser.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Admin
        });
        
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(userId: externalId, email: "admin@example.com");
        
        return (adminUser, household, token);
    }
    
    #endregion
}
