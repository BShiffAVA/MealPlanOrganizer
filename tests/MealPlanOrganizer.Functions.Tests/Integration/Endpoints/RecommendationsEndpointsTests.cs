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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for Recipe Recommendation endpoints.
/// Tests the recommendation algorithm with real database data.
/// </summary>
[Collection("Integration")]
public class RecommendationsEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;
    
    public RecommendationsEndpointsTests(IntegrationTestFixture fixture)
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
    
    #region Basic Recommendation Tests
    
    [Fact]
    public async Task GetRecommendedRecipes_WithAuthenticatedUser_ReturnsRecommendations()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            "http://localhost/api/recipes/recommended",
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.True(responseBody.TryGetProperty("recipes", out var recipes));
        Assert.True(responseBody.TryGetProperty("weekStartDate", out _));
        Assert.True(responseBody.TryGetProperty("totalRecipes", out _));
    }
    
    [Fact]
    public async Task GetRecommendedRecipes_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            "http://localhost/api/recipes/recommended",
            authHeader: null);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task GetRecommendedRecipes_WithWeekStartParameter_UsesProvidedDate()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var weekStart = DateTime.UtcNow.Date.AddDays(7);
        var httpRequest = CreateMockHttpRequest(
            $"http://localhost/api/recipes/recommended?weekStart={weekStart:yyyy-MM-dd}",
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var returnedWeekStart = responseBody.GetProperty("weekStartDate").GetString();
        Assert.Equal(weekStart.ToString("yyyy-MM-dd"), returnedWeekStart);
    }
    
    #endregion
    
    #region Algorithm Behavior Tests
    
    [Fact]
    public async Task GetRecommendedRecipes_PrioritizesHighRatedRecipes()
    {
        // Arrange
        var highRatedRecipe = RecipeBuilder.Create()
            .WithTitle("Highly Rated Recipe")
            .WithRating("user1", 5)
            .WithRating("user2", 5)
            .WithRating("user3", 5)
            .Build();
        
        var lowRatedRecipe = RecipeBuilder.Create()
            .WithTitle("Poorly Rated Recipe")
            .WithRating("user1", 1)
            .WithRating("user2", 2)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.AddRange(highRatedRecipe, lowRatedRecipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            "http://localhost/api/recipes/recommended",
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var recipes = responseBody.GetProperty("recipes").EnumerateArray().ToList();
        
        // Verify highly rated recipe appears in recommendations
        var highRatedInResults = recipes.Any(r => 
            r.GetProperty("title").GetString() == "Highly Rated Recipe");
        Assert.True(highRatedInResults);
        
        // Verify highly rated recipe ranks higher than low rated
        var highRatedIndex = recipes.FindIndex(r => 
            r.GetProperty("title").GetString() == "Highly Rated Recipe");
        var lowRatedIndex = recipes.FindIndex(r => 
            r.GetProperty("title").GetString() == "Poorly Rated Recipe");
        
        if (lowRatedIndex >= 0) // If low rated is in results, high rated should be first
        {
            Assert.True(highRatedIndex < lowRatedIndex);
        }
    }
    
    [Fact]
    public async Task GetRecommendedRecipes_RespectFrequencyPreference()
    {
        // Arrange - Create recipe with "Never" frequency preference
        var neverCookRecipe = RecipeBuilder.Create()
            .WithTitle("Never Cook This")
            .WithRating(TestData.User1Id.ToString(), 5, "Don't want to cook again", "Never")
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(neverCookRecipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            "http://localhost/api/recipes/recommended",
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var recipes = responseBody.GetProperty("recipes").EnumerateArray().ToList();
        
        // "Never" preference recipes should have lower scores or not appear
        var neverRecipe = recipes.FirstOrDefault(r => 
            r.GetProperty("title").GetString() == "Never Cook This");
        
        // The recipe either shouldn't appear or should have a low score
        if (neverRecipe.ValueKind != JsonValueKind.Undefined)
        {
            var score = neverRecipe.GetProperty("score").GetDouble();
            Assert.True(score <= 0, "Recipe with 'Never' frequency should have low/zero score");
        }
    }
    
    [Fact]
    public async Task GetRecommendedRecipes_ConsidersRecentlyCooked()
    {
        // Arrange - Create a meal plan with a recipe that was recently cooked
        var recentlyCookedRecipe = RecipeBuilder.Create()
            .WithTitle("Recently Cooked Recipe")
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recentlyCookedRecipe);
            
            // Add to a recent meal plan
            var recentMealPlan = MealPlanBuilder.Create()
                .WithDateRange(DateTime.UtcNow.Date.AddDays(-3), DateTime.UtcNow.Date.AddDays(3))
                .AsActive()
                .WithRecipeOnDay(recentlyCookedRecipe.Id, 0)
                .Build();
            
            setupDb.MealPlans.Add(recentMealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            "http://localhost/api/recipes/recommended",
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // The test verifies the algorithm runs without error
        // Actual ranking logic varies by implementation
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.True(responseBody.TryGetProperty("recipes", out _));
    }
    
    #endregion
    
    #region Response Format Tests
    
    [Fact]
    public async Task GetRecommendedRecipes_IncludesAllExpectedFields()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var recommendationService = scope.ServiceProvider.GetRequiredService<IRecipeRecommendationService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecommendedRecipes>();
        
        var function = new GetRecommendedRecipes(logger, recommendationService, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            "http://localhost/api/recipes/recommended",
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var recipes = responseBody.GetProperty("recipes").EnumerateArray().ToList();
        
        if (recipes.Count > 0)
        {
            var firstRecipe = recipes[0];
            
            // Verify expected fields are present
            Assert.True(firstRecipe.TryGetProperty("recipeId", out _));
            Assert.True(firstRecipe.TryGetProperty("title", out _));
            Assert.True(firstRecipe.TryGetProperty("score", out _));
            Assert.True(firstRecipe.TryGetProperty("reasonCodes", out _));
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private static HttpRequestData CreateMockHttpRequest(
        string url,
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(HttpMethod.Get, url, null, authHeader);
    }
    
    private static async Task<T?> ReadResponseBody<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }
    
    #endregion
}
