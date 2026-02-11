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
/// Integration tests for User and Household endpoints.
/// Tests RegisterUser, GetCurrentUser, and CreateHousehold functions.
/// </summary>
[Collection("Integration")]
public class UserHouseholdEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    
    public UserHouseholdEndpointsTests(IntegrationTestFixture fixture)
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
    
    #region RegisterUser Tests
    
    [Fact]
    public async Task RegisterUser_NewUser_Returns201AndCreatesUser()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new RegisterUser(loggerFactory, db, authHelper);
        
        var newUserId = Guid.NewGuid().ToString();
        var token = TestAuthHandler.CreateToken(
            userId: newUserId,
            email: "newuser@example.com",
            displayName: "New User");
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var userResponse = await ReadResponseBodyAsync<UserResponse>(response);
        Assert.NotNull(userResponse);
        Assert.Equal("newuser@example.com", userResponse.Email);
        Assert.Equal("New User", userResponse.DisplayName);
        Assert.Null(userResponse.Household); // New user has no household
        
        // Verify user was created in database
        var dbUser = await db.Users.FindAsync(userResponse.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(newUserId, dbUser.ExternalIdObjectId);
    }
    
    [Fact]
    public async Task RegisterUser_ExistingUser_Returns200AndUpdatesInfo()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new RegisterUser(loggerFactory, db, authHelper);
        
        // Create existing user
        var existingExternalId = Guid.NewGuid().ToString();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = existingExternalId,
            Email = "old@example.com",
            DisplayName = "Old Name",
            CreatedUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();
        
        // Call register with updated info
        var token = TestAuthHandler.CreateToken(
            userId: existingExternalId,
            email: "updated@example.com",
            displayName: "Updated Name");
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var userResponse = await ReadResponseBodyAsync<UserResponse>(response);
        Assert.NotNull(userResponse);
        Assert.Equal(existingUser.Id, userResponse.Id);
        Assert.Equal("updated@example.com", userResponse.Email);
        Assert.Equal("Updated Name", userResponse.DisplayName);
    }
    
    [Fact]
    public async Task RegisterUser_WithNoAuthHeader_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new RegisterUser(loggerFactory, db, authHelper);
        
        var httpRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: null);
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task RegisterUser_WithExpiredToken_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new RegisterUser(loggerFactory, db, authHelper);
        
        var expiredToken = TestAuthHandler.CreateExpiredToken(
            userId: Guid.NewGuid().ToString());
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            authHeader: $"Bearer {expiredToken}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task RegisterUser_UserWithHousehold_ReturnsHouseholdInfo()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new RegisterUser(loggerFactory, db, authHelper);
        
        // Create user with household
        var externalId = Guid.NewGuid().ToString();
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = "member@example.com",
            DisplayName = "Household Member",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Test Family",
            CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        db.Households.Add(household);
        
        var membership = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Admin,
            JoinedUtc = DateTime.UtcNow
        };
        db.HouseholdMembers.Add(membership);
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: "member@example.com",
            displayName: "Household Member");
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var userResponse = await ReadResponseBodyAsync<UserResponse>(response);
        Assert.NotNull(userResponse);
        Assert.NotNull(userResponse.Household);
        Assert.Equal(household.Id, userResponse.Household.Id);
        Assert.Equal("Test Family", userResponse.Household.Name);
        Assert.Equal("Admin", userResponse.Household.Role);
        Assert.Single(userResponse.Household.Members);
    }
    
    #endregion
    
    #region GetCurrentUser Tests
    
    [Fact]
    public async Task GetCurrentUser_RegisteredUser_Returns200WithUserInfo()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new GetCurrentUser(loggerFactory, db, authHelper);
        
        // Create existing user
        var externalId = Guid.NewGuid().ToString();
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = "existing@example.com",
            DisplayName = "Existing User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: "existing@example.com",
            displayName: "Existing User");
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var userResponse = await ReadResponseBodyAsync<UserResponse>(response);
        Assert.NotNull(userResponse);
        Assert.Equal(user.Id, userResponse.Id);
        Assert.Equal("existing@example.com", userResponse.Email);
        Assert.Equal("Existing User", userResponse.DisplayName);
    }
    
    [Fact]
    public async Task GetCurrentUser_UnregisteredUser_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new GetCurrentUser(loggerFactory, db, authHelper);
        
        var token = TestAuthHandler.CreateToken(
            userId: Guid.NewGuid().ToString(),
            email: "unregistered@example.com",
            displayName: "Unregistered User");
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GetCurrentUser_WithNoAuthHeader_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new GetCurrentUser(loggerFactory, db, authHelper);
        
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: null);
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task GetCurrentUser_WithHouseholdMembers_ReturnsAllMembers()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new GetCurrentUser(loggerFactory, db, authHelper);
        
        // Create two users in the same household
        var externalId1 = Guid.NewGuid().ToString();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId1,
            Email = "admin@family.com",
            DisplayName = "Family Admin",
            CreatedUtc = DateTime.UtcNow
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = "member@family.com",
            DisplayName = "Family Member",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.AddRange(user1, user2);
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Smith Family",
            CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = user1.Id
        };
        db.Households.Add(household);
        
        db.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Admin,
            JoinedUtc = DateTime.UtcNow
        });
        db.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            HouseholdId = household.Id,
            Role = HouseholdRole.Member,
            JoinedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId1,
            email: "admin@family.com",
            displayName: "Family Admin");
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var userResponse = await ReadResponseBodyAsync<UserResponse>(response);
        Assert.NotNull(userResponse);
        Assert.NotNull(userResponse.Household);
        Assert.Equal(2, userResponse.Household.Members.Count);
        Assert.Contains(userResponse.Household.Members, m => m.DisplayName == "Family Admin" && m.Role == "Admin");
        Assert.Contains(userResponse.Household.Members, m => m.DisplayName == "Family Member" && m.Role == "Member");
    }
    
    #endregion
    
    #region CreateHousehold Tests
    
    [Fact]
    public async Task CreateHousehold_ValidRequest_Returns201AndCreatesHousehold()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateHousehold(loggerFactory, db, authHelper);
        
        // Create registered user without household
        var externalId = Guid.NewGuid().ToString();
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = "creator@example.com",
            DisplayName = "Household Creator",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: "creator@example.com",
            displayName: "Household Creator");
        
        var request = new { name = "The Johnson Family" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(request),
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var householdResponse = await ReadResponseBodyAsync<HouseholdResponse>(response);
        Assert.NotNull(householdResponse);
        Assert.Equal("The Johnson Family", householdResponse.Name);
        Assert.Equal(user.Id, householdResponse.CreatedByUserId);
        
        // Verify location header
        Assert.Contains($"/api/households/{householdResponse.Id}", 
            response.Headers.GetValues("Location").FirstOrDefault());
        
        // Verify database state
        var dbHousehold = await db.Households.FindAsync(householdResponse.Id);
        Assert.NotNull(dbHousehold);
        
        var membership = db.HouseholdMembers.FirstOrDefault(
            hm => hm.HouseholdId == householdResponse.Id && hm.UserId == user.Id);
        Assert.NotNull(membership);
        Assert.Equal(HouseholdRole.Admin, membership.Role);
    }
    
    [Fact]
    public async Task CreateHousehold_UserNotRegistered_Returns404()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateHousehold(loggerFactory, db, authHelper);
        
        var token = TestAuthHandler.CreateToken(
            userId: Guid.NewGuid().ToString(),
            email: "notregistered@example.com",
            displayName: "Not Registered");
        
        var request = new { name = "Some Household" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(request),
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateHousehold_UserAlreadyInHousehold_Returns409Conflict()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateHousehold(loggerFactory, db, authHelper);
        
        // Create user already in a household
        var externalId = Guid.NewGuid().ToString();
        var uniqueEmail = $"existing-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = uniqueEmail,
            DisplayName = "Existing Member",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        
        var existingHousehold = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Existing Household",
            CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        db.Households.Add(existingHousehold);
        
        db.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HouseholdId = existingHousehold.Id,
            Role = HouseholdRole.Admin,
            JoinedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: uniqueEmail,
            displayName: "Existing Member");
        
        var request = new { name = "Another Household" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(request),
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateHousehold_MissingName_Returns400()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateHousehold(loggerFactory, db, authHelper);
        
        // Create registered user
        var externalId = Guid.NewGuid().ToString();
        var uniqueEmail = $"missing-name-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = uniqueEmail,
            DisplayName = "Some User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: uniqueEmail,
            displayName: "Some User");
        
        var request = new { name = "" }; // Empty name
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(request),
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateHousehold_WithNoAuthHeader_Returns401()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateHousehold(loggerFactory, db, authHelper);
        
        var request = new { name = "Some Household" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(request),
            authHeader: null);
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateHousehold_TrimsHouseholdName()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateHousehold(loggerFactory, db, authHelper);
        
        // Create registered user
        var externalId = Guid.NewGuid().ToString();
        var uniqueEmail = $"trim-name-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = externalId,
            Email = uniqueEmail,
            DisplayName = "Some User",
            CreatedUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: uniqueEmail,
            displayName: "Some User");
        
        var request = new { name = "  Trimmed Name  " }; // With whitespace
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(request),
            authHeader: $"Bearer {token}");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var householdResponse = await ReadResponseBodyAsync<HouseholdResponse>(response);
        Assert.NotNull(householdResponse);
        Assert.Equal("Trimmed Name", householdResponse.Name); // Trimmed
    }
    
    #endregion
    
    #region End-to-End Flow Tests
    
    [Fact]
    public async Task FullRegistrationFlow_RegisterThenCreateHousehold_Succeeds()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var registerFunction = new RegisterUser(loggerFactory, db, authHelper);
        var createHouseholdFunction = new CreateHousehold(loggerFactory, db, authHelper);
        var getCurrentUserFunction = new GetCurrentUser(loggerFactory, db, authHelper);
        
        var externalId = Guid.NewGuid().ToString();
        var token = TestAuthHandler.CreateToken(
            userId: externalId,
            email: "fullflow@example.com",
            displayName: "Full Flow User");
        
        // Step 1: Register user
        var registerRequest = CreateMockHttpRequest(HttpMethod.Post, authHeader: $"Bearer {token}");
        var registerResponse = await registerFunction.Run(registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        
        var registeredUser = await ReadResponseBodyAsync<UserResponse>(registerResponse);
        Assert.NotNull(registeredUser);
        Assert.Null(registeredUser.Household);
        
        // Step 2: Create household
        var householdRequest = new { name = "Flow Test Family" };
        var createRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: JsonSerializer.Serialize(householdRequest),
            authHeader: $"Bearer {token}");
        var createResponse = await createHouseholdFunction.Run(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        // Step 3: Get current user - should now have household
        var getUserRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {token}");
        var getUserResponse = await getCurrentUserFunction.Run(getUserRequest);
        Assert.Equal(HttpStatusCode.OK, getUserResponse.StatusCode);
        
        var currentUser = await ReadResponseBodyAsync<UserResponse>(getUserResponse);
        Assert.NotNull(currentUser);
        Assert.NotNull(currentUser.Household);
        Assert.Equal("Flow Test Family", currentUser.Household.Name);
        Assert.Equal("Admin", currentUser.Household.Role);
    }
    
    #endregion
    
    #region Helper Methods
    
    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method,
        string? body = null,
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/test", body, authHeader);
    }
    
    private static async Task<T?> ReadResponseBodyAsync<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }
    
    #endregion
}

/// <summary>
/// Response model for household creation endpoint.
/// </summary>
public class HouseholdResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
}
