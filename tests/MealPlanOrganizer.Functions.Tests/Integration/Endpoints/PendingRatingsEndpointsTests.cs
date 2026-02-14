using System.Net;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Functions;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using MealPlanOrganizer.Functions.Tests.Integration.Builders;
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
/// Integration tests for Pending Ratings endpoints.
/// Tests GetPendingRatings and UpdatePendingRating operations.
/// </summary>
[Collection("Integration")]
public class PendingRatingsEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;

    public PendingRatingsEndpointsTests(IntegrationTestFixture fixture)
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

    #region GetPendingRatings Tests

    [Fact]
    public async Task GetPendingRatings_ReturnsUserPendingRatings()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetPendingRatings>();

        var function = new GetPendingRatings(logger, db, authHelper);

        // Create test data
        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pendingRatings = await ReadResponseBody<List<PendingRatingResponse>>(response);
        Assert.NotNull(pendingRatings);
        Assert.NotEmpty(pendingRatings);
        Assert.Contains(pendingRatings, pr => pr.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task GetPendingRatings_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetPendingRatings>();

        var function = new GetPendingRatings(logger, db, authHelper);

        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: null);

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingRatings_ReturnsOnlyPendingStatus()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetPendingRatings>();

        var function = new GetPendingRatings(logger, db, authHelper);

        // Create pending rating
        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        // Create completed rating (should not be returned)
        var completedRecipe = RecipeBuilder.Create()
            .WithTitle("Completed Recipe")
            .Build();
        db.Recipes.Add(completedRecipe);
        
        // Create MealPlan and MealPlanRecipe for the completed PendingRating
        var completedMealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = "Completed Meal Plan",
            StartDate = DateTime.UtcNow.Date.AddDays(-14),
            EndDate = DateTime.UtcNow.Date.AddDays(-7),
            HouseholdId = household.Id,
            UserId = user.Id,
            Status = "Active",
            CreatedUtc = DateTime.UtcNow.AddDays(-14)
        };
        db.MealPlans.Add(completedMealPlan);

        var completedMealPlanRecipe = new MealPlanRecipe
        {
            Id = Guid.NewGuid(),
            MealPlanId = completedMealPlan.Id,
            RecipeId = completedRecipe.Id,
            Day = DateTime.UtcNow.Date.AddDays(-2),
            CreatedUtc = DateTime.UtcNow.AddDays(-14)
        };
        db.MealPlanRecipes.Add(completedMealPlanRecipe);
        
        var completedPendingRating = new PendingRating
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            RecipeId = completedRecipe.Id,
            MealPlanId = completedMealPlan.Id,
            MealPlanRecipeId = completedMealPlanRecipe.Id,
            ServedDate = DateTime.UtcNow.Date.AddDays(-1),
            Status = "Completed",
            CompletedUtc = DateTime.UtcNow.AddHours(-1),
            CreatedUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.PendingRatings.Add(completedPendingRating);
        await db.SaveChangesAsync();

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pendingRatings = await ReadResponseBody<List<PendingRatingResponse>>(response);
        Assert.NotNull(pendingRatings);
        Assert.DoesNotContain(pendingRatings, pr => pr.RecipeId == completedRecipe.Id);
    }

    [Fact]
    public async Task GetPendingRatings_IncludesRecipeDetails()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetPendingRatings>();

        var function = new GetPendingRatings(logger, db, authHelper);

        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        var pendingRatings = await ReadResponseBody<List<PendingRatingResponse>>(response);
        var foundRating = pendingRatings?.FirstOrDefault(pr => pr.RecipeId == recipe.Id);

        Assert.NotNull(foundRating);
        Assert.Equal(recipe.Title, foundRating.RecipeTitle);
        Assert.Equal(recipe.CuisineType, foundRating.CuisineType);
    }

    [Fact]
    public async Task GetPendingRatings_NoRatings_ReturnsEmptyArray()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetPendingRatings>();

        var function = new GetPendingRatings(logger, db, authHelper);

        // Create user with no pending ratings
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = "noratings@test.com",
            DisplayName = "No Ratings User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pendingRatings = await ReadResponseBody<List<PendingRatingResponse>>(response);
        Assert.NotNull(pendingRatings);
        Assert.Empty(pendingRatings);
    }

    #endregion

    #region UpdatePendingRating Tests

    [Fact]
    public async Task UpdatePendingRating_MarkAsCompleted_Succeeds()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        var updateRequest = new { status = "Completed" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, pendingRating.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify in database
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedRating = await verifyDb.PendingRatings.FindAsync(pendingRating.Id);
        Assert.NotNull(updatedRating);
        Assert.Equal("Completed", updatedRating.Status);
        Assert.NotNull(updatedRating.CompletedUtc);
    }

    [Fact]
    public async Task UpdatePendingRating_MarkAsDismissed_Succeeds()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        var updateRequest = new { status = "Dismissed" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, pendingRating.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedRating = await verifyDb.PendingRatings.FindAsync(pendingRating.Id);
        Assert.NotNull(updatedRating);
        Assert.Equal("Dismissed", updatedRating.Status);
    }

    [Fact]
    public async Task UpdatePendingRating_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        var updateRequest = new { status = "Completed" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            authHeader: null);

        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePendingRating_InvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        var updateRequest = new { status = "InvalidStatus" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, pendingRating.Id);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePendingRating_MissingStatus_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        var (user, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            "{}",
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, pendingRating.Id);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePendingRating_NonExistentRating_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        // Create user
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updateRequest = new { status = "Completed" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePendingRating_OtherUserRating_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UpdatePendingRating>();

        var function = new UpdatePendingRating(logger, db, authHelper);

        // Create pending rating for one user
        var (originalUser, household, recipe, pendingRating) = await CreatePendingRatingAsync(db);

        // Create another user trying to access the rating
        var anotherUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = "another@test.com",
            DisplayName = "Another User"
        };
        db.Users.Add(anotherUser);
        await db.SaveChangesAsync();

        var updateRequest = new { status = "Completed" };
        var token = TestAuthHandler.CreateToken(anotherUser.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Patch,
            JsonSerializer.Serialize(updateRequest),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, pendingRating.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method,
        string? body = null,
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/ratings/pending", body, authHeader);
    }

    private static async Task<T?> ReadResponseBody<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }

    private async Task<(User user, Household household, Recipe recipe, PendingRating pendingRating)> CreatePendingRatingAsync(AppDbContext db)
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

        household.CreatedByUserId = user.Id;

        var recipe = RecipeBuilder.Create()
            .WithTitle("Test Recipe for Rating")
            .WithCuisineType("Italian")
            .Build();

        // Create MealPlan and MealPlanRecipe to satisfy FK constraints
        var mealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Meal Plan",
            StartDate = DateTime.UtcNow.Date.AddDays(-7),
            EndDate = DateTime.UtcNow.Date,
            HouseholdId = household.Id,
            UserId = user.Id,
            Status = "Active",
            CreatedUtc = DateTime.UtcNow
        };

        var mealPlanRecipe = new MealPlanRecipe
        {
            Id = Guid.NewGuid(),
            MealPlanId = mealPlan.Id,
            RecipeId = recipe.Id,
            Day = DateTime.UtcNow.Date.AddDays(-1),
            CreatedUtc = DateTime.UtcNow
        };

        var pendingRating = new PendingRating
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            RecipeId = recipe.Id,
            MealPlanId = mealPlan.Id,
            MealPlanRecipeId = mealPlanRecipe.Id,
            ServedDate = DateTime.UtcNow.Date,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow
        };

        db.Households.Add(household);
        db.Users.Add(user);
        db.Recipes.Add(recipe);
        db.MealPlans.Add(mealPlan);
        db.MealPlanRecipes.Add(mealPlanRecipe);
        db.PendingRatings.Add(pendingRating);
        await db.SaveChangesAsync();

        return (user, household, recipe, pendingRating);
    }

    #endregion
}
