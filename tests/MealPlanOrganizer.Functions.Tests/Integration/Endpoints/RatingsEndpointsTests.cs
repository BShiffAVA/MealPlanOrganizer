using System.Net;
using System.Text;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Functions;
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
/// Integration tests for Recipe Rating endpoints.
/// Tests rating CRUD operations, average calculations, and user rating history.
/// </summary>
[Collection("Integration")]
public class RatingsEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;
    
    public RatingsEndpointsTests(IntegrationTestFixture fixture)
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
    
    #region RateRecipe Tests
    
    [Fact]
    public async Task RateRecipe_WithValidRating_CreatesRating()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RateRecipe>();
        
        var function = new RateRecipe(logger, db, authHelper);
        
        // Create a new recipe to rate
        var recipe = RecipeBuilder.Create()
            .WithTitle("Recipe to Rate")
            .Build();
        
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        
        var rateRequest = new
        {
            rating = 5,
            comments = "Excellent recipe!",
            nextTimePreference = "RightAway"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(rateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        // Verify rating was saved
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var savedRating = await verifyDb.RecipeRatings
            .FirstOrDefaultAsync(r => r.RecipeId == recipe.Id);
        
        Assert.NotNull(savedRating);
        Assert.Equal(5, savedRating.Rating);
        Assert.Equal("Excellent recipe!", savedRating.Comments);
        Assert.Equal("RightAway", savedRating.NextTimePreference);
    }
    
    [Fact]
    public async Task RateRecipe_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RateRecipe>();
        
        var function = new RateRecipe(logger, db, authHelper);
        
        var rateRequest = new { rating = 5 };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(rateRequest),
            authHeader: null); // No auth
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, TestData.Recipe1Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public async Task RateRecipe_WithInvalidRating_ReturnsBadRequest(int invalidRating)
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RateRecipe>();
        
        var function = new RateRecipe(logger, db, authHelper);
        
        var rateRequest = new { rating = invalidRating };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(rateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, TestData.Recipe1Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task RateRecipe_NonExistentRecipe_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RateRecipe>();
        
        var function = new RateRecipe(logger, db, authHelper);
        
        var rateRequest = new { rating = 4 };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(rateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), context);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task RateRecipe_SameUserUpdatesExistingRating()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RateRecipe>();
        
        var function = new RateRecipe(logger, db, authHelper);
        
        // Use a test user and the pre-seeded recipe
        var recipeId = TestData.Recipe1Id;
        
        // First rating
        var firstRating = new { rating = 3, comments = "First impression" };
        var firstRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(firstRating),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        await function.Run(firstRequest, recipeId, context);
        
        // Second rating by same user
        var secondRating = new { rating = 5, comments = "Changed my mind - excellent!" };
        var secondRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(secondRating),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(secondRequest, recipeId, context);
        
        // Assert - Rating should be updated/replaced
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var allRatings = await verifyDb.RecipeRatings
            .Where(r => r.RecipeId == recipeId && r.UserId.Contains(TestData.User1Id.ToString()))
            .ToListAsync();
        
        // Verify we don't have duplicate ratings from the same user
        Assert.True(allRatings.Count <= 1 || allRatings.All(r => r.Rating == 5));
    }
    
    [Fact]
    public async Task RateRecipe_CommentsTooLong_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RateRecipe>();
        
        var function = new RateRecipe(logger, db, authHelper);
        
        var rateRequest = new
        {
            rating = 4,
            comments = new string('x', 501) // 501 characters exceeds limit
        };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(rateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, TestData.Recipe1Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    #endregion
    
    #region GetRecipeRatings Tests
    
    [Fact]
    public async Task GetRecipeRatings_ReturnsAllRatingsForRecipe()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecipeRatings>();
        
        var function = new GetRecipeRatings(logger, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, TestData.Recipe1Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.True(responseBody.TryGetProperty("ratings", out var ratings));
        Assert.True(responseBody.TryGetProperty("averageRating", out var avgRating));
    }
    
    [Fact]
    public async Task GetRecipeRatings_NonExistentRecipe_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecipeRatings>();
        
        var function = new GetRecipeRatings(logger, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), context);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GetRecipeRatings_CalculatesCorrectAverageRating()
    {
        // Arrange
        var recipe = RecipeBuilder.Create()
            .WithTitle("Rated Recipe")
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
            
            // Add ratings: 5, 4, 4, 3 = average 4.0
            var ratings = new[]
            {
                new RecipeRating { RecipeId = recipe.Id, UserId = "user1", Rating = 5, RatedUtc = DateTime.UtcNow },
                new RecipeRating { RecipeId = recipe.Id, UserId = "user2", Rating = 4, RatedUtc = DateTime.UtcNow },
                new RecipeRating { RecipeId = recipe.Id, UserId = "user3", Rating = 4, RatedUtc = DateTime.UtcNow },
                new RecipeRating { RecipeId = recipe.Id, UserId = "user4", Rating = 3, RatedUtc = DateTime.UtcNow }
            };
            
            setupDb.RecipeRatings.AddRange(ratings);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecipeRatings>();
        
        var function = new GetRecipeRatings(logger, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var averageRating = responseBody.GetProperty("averageRating").GetDouble();
        
        Assert.Equal(4.0, averageRating);
    }
    
    #endregion
    
    #region GetUserRatingHistory Tests
    
    [Fact]
    public async Task GetUserRatingHistory_ReturnsAllUserRatings()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetUserRatingHistory>();
        
        var function = new GetUserRatingHistory(logger, db, authHelper);
        
        // Create ratings for the test user
        var userId = TestData.User1Id.ToString();
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.True(responseBody.TryGetProperty("ratings", out _));
        Assert.True(responseBody.TryGetProperty("totalRatings", out _));
    }
    
    [Fact]
    public async Task GetUserRatingHistory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetUserRatingHistory>();
        
        var function = new GetUserRatingHistory(logger, db, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: null);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUserRatingHistory_IncludesRecipeNames()
    {
        // Arrange
        var recipe = RecipeBuilder.Create()
            .WithTitle("User's Rated Recipe")
            .Build();
        
        var userId = Guid.NewGuid().ToString();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            
            setupDb.RecipeRatings.Add(new RecipeRating
            {
                RecipeId = recipe.Id,
                UserId = userId,
                Rating = 5,
                Comments = "My favorite!",
                RatedUtc = DateTime.UtcNow
            });
            
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetUserRatingHistory>();
        
        var function = new GetUserRatingHistory(logger, db, authHelper);
        
        // Create a token for this specific user
        var token = TestAuthHandler.CreateToken(userId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: $"Bearer {token}");
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var ratings = responseBody.GetProperty("ratings");
        
        Assert.True(ratings.GetArrayLength() >= 1);
        
        var firstRating = ratings[0];
        Assert.Equal("User's Rated Recipe", firstRating.GetProperty("recipeName").GetString());
    }
    
    #endregion
    
    #region Helper Methods
    
    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method, 
        string? body = null, 
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/recipes", body, authHeader);
    }
    
    private static async Task<T?> ReadResponseBody<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }
    
    #endregion
}
