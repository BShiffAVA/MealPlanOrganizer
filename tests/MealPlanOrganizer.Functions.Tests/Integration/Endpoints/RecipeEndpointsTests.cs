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
/// Integration tests for Recipe CRUD endpoints.
/// Tests the full function execution flow with real database (Testcontainer).
/// </summary>
[Collection("Integration")]
public class RecipeEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;
    
    public RecipeEndpointsTests(IntegrationTestFixture fixture)
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
    
    #region CreateRecipe Tests
    
    [Fact]
    public async Task CreateRecipe_WithValidData_ReturnsCreatedRecipe()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateRecipe(loggerFactory, db, authHelper);
        
        var request = new
        {
            title = "Integration Test Recipe",
            description = "A recipe created during integration testing",
            cuisineType = "Test",
            prepTimeMinutes = 15,
            cookTimeMinutes = 30,
            servings = 4,
            ingredients = new[]
            {
                new { name = "Test Ingredient 1", quantity = "1 cup" },
                new { name = "Test Ingredient 2", quantity = "2 tbsp" }
            },
            steps = new[] { "First step", "Second step" }
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var responseBody = await ReadResponseBody<dynamic>(response);
        Assert.NotNull(responseBody);
        
        // Verify recipe was saved to database
        var savedRecipe = await _db.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Title == "Integration Test Recipe");
        
        Assert.NotNull(savedRecipe);
        Assert.Equal("A recipe created during integration testing", savedRecipe.Description);
        Assert.Equal(2, savedRecipe.Ingredients.Count);
        Assert.Equal(2, savedRecipe.Steps.Count);
    }
    
    [Fact]
    public async Task CreateRecipe_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateRecipe(loggerFactory, db, authHelper);
        
        var request = new { title = "Test Recipe" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            authHeader: null); // No auth header
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateRecipe_WithMissingTitle_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new CreateRecipe(loggerFactory, db, authHelper);
        
        var request = new { description = "Missing title" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    #endregion
    
    #region GetRecipeById Tests
    
    [Fact]
    public async Task GetRecipeById_ExistingRecipe_ReturnsRecipeWithDetails()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecipeById>();
        
        // Use the pre-seeded test recipe
        var recipeId = TestData.Recipe1Id;
        
        var function = new GetRecipeById(logger, db, blobUrlService, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        // Act
        var response = await function.Run(httpRequest, recipeId);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.Equal(recipeId.ToString(), responseBody.GetProperty("id").GetString());
        Assert.NotNull(responseBody.GetProperty("title").GetString());
    }
    
    [Fact]
    public async Task GetRecipeById_NonExistentRecipe_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecipeById>();
        
        var function = new GetRecipeById(logger, db, blobUrlService, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        var nonExistentId = Guid.NewGuid();
        
        // Act
        var response = await function.Run(httpRequest, nonExistentId);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GetRecipeById_ReturnsIngredientsAndSteps()
    {
        // Arrange
        var recipe = RecipeBuilder.Create()
            .WithTitle("Recipe With Details")
            .WithFullDetails()
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetRecipeById>();
        
        var function = new GetRecipeById(logger, db, blobUrlService, authHelper);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var ingredients = responseBody.GetProperty("ingredients");
        var steps = responseBody.GetProperty("steps");
        
        Assert.True(ingredients.GetArrayLength() > 0);
        Assert.True(steps.GetArrayLength() > 0);
    }
    
    #endregion
    
    #region ListRecipes Tests
    
    [Fact]
    public async Task ListRecipes_ReturnsSeededRecipes()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        
        var function = new ListRecipes(loggerFactory, db, blobUrlService);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement[]>(response);
        Assert.NotNull(responseBody);
        Assert.True(responseBody.Length >= 3); // At least 3 seeded recipes
    }
    
    [Fact]
    public async Task ListRecipes_IncludesAverageRating()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        
        var function = new ListRecipes(loggerFactory, db, blobUrlService);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get);
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement[]>(response);
        
        // Find the rated recipe and verify it has an average rating
        var ratedRecipe = responseBody.FirstOrDefault(r => 
            r.GetProperty("id").GetString() == TestData.Recipe1Id.ToString());
        
        Assert.True(ratedRecipe.ValueKind != JsonValueKind.Undefined);
        Assert.True(ratedRecipe.TryGetProperty("averageRating", out _));
    }
    
    #endregion
    
    #region UpdateRecipe Tests
    
    [Fact]
    public async Task UpdateRecipe_WithValidData_UpdatesRecipe()
    {
        // Arrange
        var recipe = RecipeBuilder.Create()
            .WithTitle("Original Title")
            .WithDescription("Original description")
            .WithCreatedByUserId(TestData.User1InternalId)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateRecipe(loggerFactory, db, authHelper);
        
        var updateRequest = new
        {
            title = "Updated Title",
            description = "Updated description"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Put,
            JsonSerializer.Serialize(updateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id.ToString());
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify database was updated
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedRecipe = await verifyDb.Recipes.FindAsync(recipe.Id);
        
        Assert.NotNull(updatedRecipe);
        Assert.Equal("Updated Title", updatedRecipe.Title);
        Assert.Equal("Updated description", updatedRecipe.Description);
    }
    
    [Fact]
    public async Task UpdateRecipe_NonExistentRecipe_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateRecipe(loggerFactory, db, authHelper);
        
        var updateRequest = new { title = "New Title" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Put,
            JsonSerializer.Serialize(updateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid().ToString());
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateRecipe_ByNonCreator_ReturnsForbidden()
    {
        // Arrange - Create recipe by User1
        var recipe = RecipeBuilder.Create()
            .WithTitle("User1 Recipe")
            .WithCreatedBy(TestUsers.User1.UserId)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateRecipe(loggerFactory, db, authHelper);
        
        var updateRequest = new { title = "Attempted Update By User2" };
        
        // Act - Attempt to update with User2's credentials
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Put,
            JsonSerializer.Serialize(updateRequest),
            TestUsers.User2.CreateAuthHeader().ToString());
        
        var response = await function.Run(httpRequest, recipe.Id.ToString());
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.Contains("creator", responseBody.GetProperty("error").GetString()?.ToLower() ?? string.Empty);
        
        // Verify recipe was not modified
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var unchangedRecipe = await verifyDb.Recipes.FindAsync(recipe.Id);
        Assert.NotNull(unchangedRecipe);
        Assert.Equal("User1 Recipe", unchangedRecipe.Title);
    }
    
    [Fact]
    public async Task UpdateRecipe_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var recipe = RecipeBuilder.Create()
            .WithTitle("Recipe to Update")
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateRecipe(loggerFactory, db, authHelper);
        
        var updateRequest = new { title = "Unauthenticated Update" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Put,
            JsonSerializer.Serialize(updateRequest),
            authHeader: null); // No auth header
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id.ToString());
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateRecipe_PreservesRatings()
    {
        // Arrange - Create recipe with ratings
        var recipe = RecipeBuilder.Create()
            .WithTitle("Recipe With Ratings")
            .WithDescription("Original description")
            .WithCreatedBy(TestUsers.User1.UserId)
            .WithCreatedByUserId(TestData.User1InternalId)
            .WithRating(TestUsers.User1.UserId, 5, "Excellent!")
            .WithRating(TestUsers.User2.UserId, 4, "Very good")
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        // Verify ratings exist before update
        await using (var verifyBeforeDb = _fixture.TestHost.CreateDbContext())
        {
            var ratingsBefore = await verifyBeforeDb.RecipeRatings
                .Where(r => r.RecipeId == recipe.Id)
                .ToListAsync();
            Assert.Equal(2, ratingsBefore.Count);
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateRecipe(loggerFactory, db, authHelper);
        
        var updateRequest = new
        {
            title = "Updated Recipe With Ratings",
            description = "Updated description",
            ingredients = new[]
            {
                new { name = "New Ingredient", quantity = "1 cup" }
            },
            steps = new[] { "New step 1", "New step 2" }
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Put,
            JsonSerializer.Serialize(updateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id.ToString());
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify recipe was updated
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedRecipe = await verifyDb.Recipes
            .Include(r => r.Ratings)
            .FirstOrDefaultAsync(r => r.Id == recipe.Id);
        
        Assert.NotNull(updatedRecipe);
        Assert.Equal("Updated Recipe With Ratings", updatedRecipe.Title);
        Assert.Equal("Updated description", updatedRecipe.Description);
        
        // Verify ratings are preserved
        Assert.Equal(2, updatedRecipe.Ratings.Count);
        Assert.Contains(updatedRecipe.Ratings, r => r.UserId == TestUsers.User1.UserId && r.Rating == 5);
        Assert.Contains(updatedRecipe.Ratings, r => r.UserId == TestUsers.User2.UserId && r.Rating == 4);
    }
    
    [Fact]
    public async Task UpdateRecipe_ByCreator_Succeeds()
    {
        // Arrange - Create recipe explicitly by User1
        var recipe = RecipeBuilder.Create()
            .WithTitle("Creator's Recipe")
            .WithCreatedBy(TestUsers.User1.UserId)
            .WithCreatedByUserId(TestData.User1InternalId)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var function = new UpdateRecipe(loggerFactory, db, authHelper);
        
        var updateRequest = new
        {
            title = "Updated By Creator",
            description = "Creator updated this"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Put,
            JsonSerializer.Serialize(updateRequest),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.Run(httpRequest, recipe.Id.ToString());
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedRecipe = await verifyDb.Recipes.FindAsync(recipe.Id);
        Assert.NotNull(updatedRecipe);
        Assert.Equal("Updated By Creator", updatedRecipe.Title);
    }
    
    #endregion
    
    #region Cascade Delete Tests
    
    [Fact]
    public async Task DeleteRecipe_CascadesDeleteToIngredientsAndSteps()
    {
        // Arrange
        var recipe = RecipeBuilder.Create()
            .WithTitle("Recipe to Delete")
            .WithFullDetails()
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }
        
        var recipeId = recipe.Id;
        var ingredientCount = recipe.Ingredients.Count;
        var stepCount = recipe.Steps.Count;
        
        // Verify ingredients and steps exist
        await using (var verifyDb = _fixture.TestHost.CreateDbContext())
        {
            var ingredientsBefore = await verifyDb.Set<RecipeIngredient>()
                .Where(i => i.RecipeId == recipeId)
                .CountAsync();
            var stepsBefore = await verifyDb.Set<RecipeStep>()
                .Where(s => s.RecipeId == recipeId)
                .CountAsync();
            
            Assert.Equal(ingredientCount, ingredientsBefore);
            Assert.Equal(stepCount, stepsBefore);
        }
        
        // Act - Delete the recipe directly
        await using (var deleteDb = _fixture.TestHost.CreateDbContext())
        {
            var recipeToDelete = await deleteDb.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.Steps)
                .Include(r => r.Ratings)
                .FirstAsync(r => r.Id == recipeId);
            
            deleteDb.Recipes.Remove(recipeToDelete);
            await deleteDb.SaveChangesAsync();
        }
        
        // Assert - Verify cascade delete
        await using var assertDb = _fixture.TestHost.CreateDbContext();
        
        var deletedRecipe = await assertDb.Recipes.FindAsync(recipeId);
        Assert.Null(deletedRecipe);
        
        var ingredientsAfter = await assertDb.Set<RecipeIngredient>()
            .Where(i => i.RecipeId == recipeId)
            .CountAsync();
        var stepsAfter = await assertDb.Set<RecipeStep>()
            .Where(s => s.RecipeId == recipeId)
            .CountAsync();
        
        Assert.Equal(0, ingredientsAfter);
        Assert.Equal(0, stepsAfter);
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
