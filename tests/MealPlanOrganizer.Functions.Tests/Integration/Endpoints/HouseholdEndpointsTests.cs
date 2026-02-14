using System.Net;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Functions;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using MealPlanOrganizer.Functions.Tests.Integration.Fixtures;
using MealPlanOrganizer.Functions.Tests.Integration.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for Household management endpoints.
/// Tests household update (name, timezone) and timezone listing.
/// </summary>
[Collection("Integration")]
public class HouseholdEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;

    public HouseholdEndpointsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = _fixture.TestHost.CreateDbContext();
        await _fixture.TestHost.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region GetTimezones Tests

    [Fact]
    public async Task GetTimezones_ReturnsListOfTimezones()
    {
        // Arrange
        var function = new GetTimezones();
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);

        // Act
        var response = function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var timezones = await ReadResponseBody<string[]>(response);
        Assert.NotNull(timezones);
        Assert.NotEmpty(timezones);
        Assert.Contains("America/New_York", timezones);
        Assert.Contains("America/Los_Angeles", timezones);
        Assert.Contains("Europe/London", timezones);
        Assert.Contains("Asia/Tokyo", timezones);
    }

    [Fact]
    public async Task GetTimezones_ReturnsCommonTimezonesArray()
    {
        // Arrange
        var function = new GetTimezones();
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);

        // Act
        var response = function.Run(httpRequest);

        // Assert
        var timezones = await ReadResponseBody<string[]>(response);
        Assert.NotNull(timezones);
        
        // Should match the CommonTimeZones array
        Assert.Equal(UpdateHouseholdRequest.CommonTimeZones.Length, timezones.Length);
    }

    #endregion

    #region UpdateHousehold Tests

    [Fact]
    public async Task UpdateHousehold_WithValidName_UpdatesHousehold()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        // Create user and household
        var (user, household) = await CreateUserWithHouseholdAsync(db, isAdmin: true);

        var updateRequest = new { name = "Updated Household Name" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, household.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await ReadResponseBody<HouseholdResponse>(response);
        Assert.NotNull(responseBody);
        Assert.Equal("Updated Household Name", responseBody.Name);
    }

    [Fact]
    public async Task UpdateHousehold_WithValidTimezone_UpdatesHousehold()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        var (user, household) = await CreateUserWithHouseholdAsync(db, isAdmin: true);

        var updateRequest = new { timeZoneId = "America/Los_Angeles" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, household.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.Equal("America/Los_Angeles", responseBody.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task UpdateHousehold_WithInvalidTimezone_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        var (user, household) = await CreateUserWithHouseholdAsync(db, isAdmin: true);

        var updateRequest = new { timeZoneId = "Invalid/Timezone" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, household.Id);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHousehold_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        var updateRequest = new { name = "New Name" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            authHeader: null);

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHousehold_NonAdminUser_ReturnsForbidden()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        // Create user as non-admin member
        var (user, household) = await CreateUserWithHouseholdAsync(db, isAdmin: false);

        var updateRequest = new { name = "New Name" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, household.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHousehold_NotMemberOfHousehold_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        // Create a user without a household
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = "standalone@test.com",
            DisplayName = "Standalone User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updateRequest = new { name = "New Name" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHousehold_EmptyRequest_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        var (user, household) = await CreateUserWithHouseholdAsync(db, isAdmin: true);

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            "{}",
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, household.Id);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("America/Chicago")]
    [InlineData("America/Denver")]
    [InlineData("America/Los_Angeles")]
    [InlineData("Europe/London")]
    [InlineData("Europe/Paris")]
    [InlineData("Asia/Tokyo")]
    [InlineData("Australia/Sydney")]
    public async Task UpdateHousehold_WithCommonTimezones_Succeeds(string timezone)
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var function = new UpdateHousehold(loggerFactory, db, authHelper);

        var (user, household) = await CreateUserWithHouseholdAsync(db, isAdmin: true);

        var updateRequest = new { timeZoneId = timezone };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        var context = new Mock<FunctionContext>().Object;

        // Act
        var response = await function.Run(httpRequest, household.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.Equal(timezone, responseBody.GetProperty("timeZoneId").GetString());
    }

    #endregion

    #region Helper Methods

    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method,
        string? body = null,
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/households", body, authHeader);
    }

    private static async Task<T?> ReadResponseBody<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }

    private async Task<(User user, Household household)> CreateUserWithHouseholdAsync(AppDbContext db, bool isAdmin)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Test Household",
            TimeZoneId = "America/New_York",
            CreatedUtc = DateTime.UtcNow
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = $"test-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User"
        };

        var membership = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            Role = isAdmin ? HouseholdRole.Admin : HouseholdRole.Member,
            JoinedUtc = DateTime.UtcNow
        };

        household.CreatedByUserId = user.Id;

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMembers.Add(membership);
        await db.SaveChangesAsync();

        return (user, household);
    }

    #endregion
}
