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
/// Integration tests for Member Management endpoints.
/// Tests RemoveMember and UpdateMemberWeight functions.
/// </summary>
[Collection("Integration")]
public class MemberManagementEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    
    public MemberManagementEndpointsTests(IntegrationTestFixture fixture)
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
    
    #region RemoveMember Tests
    
    [Fact]
    public async Task RemoveMember_AsAdmin_RemovesMemberSuccessfully()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify member was removed
        var membership = db.HouseholdMembers.FirstOrDefault(hm => hm.UserId == memberUser.Id);
        Assert.Null(membership);
    }
    
    [Fact]
    public async Task RemoveMember_AsNonAdmin_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, memberToken) = await AddMemberToHouseholdAsync(db, household);
        
        // Try to remove the admin as a member
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {memberToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, adminUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task RemoveMember_LastAdmin_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act - admin tries to remove themselves
        var response = await function.Run(httpRequest, household.Id, adminUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await ReadResponseBodyAsStringAsync(response);
        Assert.Contains("only admin", message, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task RemoveMember_MemberNotFound_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, Guid.NewGuid());
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task RemoveMember_HouseholdNotFound_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, _, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), adminUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task RemoveMember_NoAuth_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: null);
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), Guid.NewGuid());
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task RemoveMember_NonHouseholdMember_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, _) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        // Create a user outside this household
        var outsiderExternalId = Guid.NewGuid().ToString();
        var outsider = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = outsiderExternalId,
            Email = "outsider@example.com",
            DisplayName = "Outsider User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(outsider);
        await db.SaveChangesAsync();
        
        var outsiderToken = TestAuthHandler.CreateToken(userId: outsiderExternalId, email: "outsider@example.com");
        
        var function = new RemoveMember(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {outsiderToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    #endregion
    
    #region UpdateMemberWeight Tests
    
    [Fact]
    public async Task UpdateMemberWeight_AsAdmin_UpdatesSuccessfully()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 5 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var memberResponse = await ReadResponseBodyAsync<HouseholdMemberInfo>(response);
        Assert.NotNull(memberResponse);
        Assert.Equal(5, memberResponse.Weight);
        Assert.Equal(memberUser.Id, memberResponse.UserId);
        
        // Verify database was updated
        var membership = db.HouseholdMembers.First(hm => hm.UserId == memberUser.Id);
        Assert.Equal(5, membership.Weight);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_AdminUpdatesOwnWeight_Succeeds()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 1 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, adminUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var memberResponse = await ReadResponseBodyAsync<HouseholdMemberInfo>(response);
        Assert.NotNull(memberResponse);
        Assert.Equal(1, memberResponse.Weight);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_AsNonAdmin_Returns403()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, _) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, memberToken) = await AddMemberToHouseholdAsync(db, household);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 5 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: $"Bearer {memberToken}");
        
        // Act - member tries to update their own weight
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_InvalidWeightTooLow_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 0 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await ReadResponseBodyAsStringAsync(response);
        Assert.Contains("between 1 and 5", message, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_InvalidWeightTooHigh_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 6 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_MemberNotFound_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 3 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, Guid.NewGuid());
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_NoBody_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (_, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: null, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest, household.Id, memberUser.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_NoAuth_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateMemberWeight(loggerFactory, db, authHelper);
        var requestBody = JsonSerializer.Serialize(new { weight = 3 });
        var httpRequest = CreateMockHttpRequest(HttpMethod.Patch, body: requestBody, authHeader: null);
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), Guid.NewGuid());
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateMemberWeight_DefaultWeightIs3()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        
        // Get admin's weight (should be default 3)
        var membership = db.HouseholdMembers.First(hm => hm.UserId == adminUser.Id);
        
        // Assert
        Assert.Equal(3, membership.Weight);
    }
    
    #endregion
    
    #region GetCurrentUser Weight Integration Tests
    
    [Fact]
    public async Task GetCurrentUser_ReturnsWeightInMemberInfo()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var (adminUser, household, adminToken) = await CreateAdminWithHouseholdAsync(db);
        var (memberUser, _) = await AddMemberToHouseholdAsync(db, household);
        
        // Update member weight to 5
        var memberMembership = db.HouseholdMembers.First(hm => hm.UserId == memberUser.Id);
        memberMembership.Weight = 5;
        await db.SaveChangesAsync();
        
        var function = new GetCurrentUser(loggerFactory, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {adminToken}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var userResponse = await ReadResponseBodyAsync<UserResponse>(response);
        Assert.NotNull(userResponse);
        Assert.NotNull(userResponse.Household);
        Assert.Equal(2, userResponse.Household.Members.Count);
        
        var admin = userResponse.Household.Members.First(m => m.UserId == adminUser.Id);
        Assert.Equal(3, admin.Weight); // Default
        
        var member = userResponse.Household.Members.First(m => m.UserId == memberUser.Id);
        Assert.Equal(5, member.Weight); // Updated
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
    
    private static async Task<string> ReadResponseBodyAsStringAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        return await reader.ReadToEndAsync();
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
    
    private async Task<(User memberUser, string token)> AddMemberToHouseholdAsync(AppDbContext db, Household household)
    {
        var externalId = Guid.NewGuid().ToString();
        var memberUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
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
        
        var token = TestAuthHandler.CreateToken(userId: externalId, email: "member@example.com");
        
        return (memberUser, token);
    }
    
    #endregion
}
