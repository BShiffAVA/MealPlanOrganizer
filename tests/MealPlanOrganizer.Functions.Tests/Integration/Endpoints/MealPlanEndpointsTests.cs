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
/// Integration tests for Meal Plan endpoints.
/// Tests meal plan CRUD operations and recipe assignments.
/// </summary>
[Collection("Integration")]
public class MealPlanEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;
    
    public MealPlanEndpointsTests(IntegrationTestFixture fixture)
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
    
    #region CreateMealPlan Tests
    
    [Fact]
    public async Task CreateMealPlan_WithValidData_CreatesMealPlan()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CreateMealPlan>();
        
        var function = new CreateMealPlan(logger, db, authHelper);
        
        var startDate = DateTime.UtcNow.Date;
        var request = new
        {
            name = "Test Week Meal Plan",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = startDate.AddDays(6).ToString("yyyy-MM-dd")
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        // Verify saved to database
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var savedPlan = await verifyDb.MealPlans
            .FirstOrDefaultAsync(mp => mp.Name == "Test Week Meal Plan");
        
        Assert.NotNull(savedPlan);
        Assert.Equal("Draft", savedPlan.Status);
    }
    
    [Fact]
    public async Task CreateMealPlan_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CreateMealPlan>();
        
        var function = new CreateMealPlan(logger, db, authHelper);
        
        var request = new { name = "Test Plan", startDate = DateTime.UtcNow.ToString("yyyy-MM-dd") };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            authHeader: null);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateMealPlan_WithMissingName_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CreateMealPlan>();
        
        var function = new CreateMealPlan(logger, db, authHelper);
        
        var request = new { startDate = DateTime.UtcNow.ToString("yyyy-MM-dd") };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateMealPlan_WithEndDateBeforeStartDate_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CreateMealPlan>();
        
        var function = new CreateMealPlan(logger, db, authHelper);
        
        var startDate = DateTime.UtcNow.Date;
        var request = new
        {
            name = "Invalid Plan",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = startDate.AddDays(-1).ToString("yyyy-MM-dd") // End before start
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    #endregion
    
    #region ListMealPlans Tests
    
    [Fact]
    public async Task ListMealPlans_ReturnsAllMealPlans()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ListMealPlans>();
        
        var function = new ListMealPlans(logger, db, authHelper);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.True(responseBody.TryGetProperty("mealPlans", out var plans));
        Assert.True(responseBody.TryGetProperty("totalMealPlans", out _));
    }
    
    [Fact]
    public async Task ListMealPlans_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ListMealPlans>();
        
        var function = new ListMealPlans(logger, db, authHelper);
        
        var httpRequest = CreateMockHttpRequest(HttpMethod.Get, authHeader: null);
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task ListMealPlans_IncludesRecipeCount()
    {
        // Arrange
        var mealPlan = MealPlanBuilder.CreateForCurrentWeek()
            .WithRecipeOnMonday(TestData.Recipe1Id)
            .WithRecipeOnTuesday(TestData.Recipe2Id)
            .WithRecipeOnWednesday(TestData.Recipe3Id)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.MealPlans.Add(mealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ListMealPlans>();
        
        var function = new ListMealPlans(logger, db, authHelper);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        var plans = responseBody.GetProperty("mealPlans");
        
        var createdPlan = plans.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("id").GetString() == mealPlan.Id.ToString());
        
        Assert.True(createdPlan.ValueKind != JsonValueKind.Undefined);
        Assert.Equal(3, createdPlan.GetProperty("recipesAssigned").GetInt32());
    }
    
    #endregion
    
    #region AddRecipeToMealPlan Tests
    
    [Fact]
    public async Task AddRecipeToMealPlan_WithValidData_AddsRecipe()
    {
        // Arrange
        var mealPlan = MealPlanBuilder.CreateForCurrentWeek().Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.MealPlans.Add(mealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<AddRecipeToMealPlan>();
        
        var function = new AddRecipeToMealPlan(logger, db, authHelper);
        
        var request = new
        {
            recipeId = TestData.Recipe1Id,
            day = mealPlan.StartDate.AddDays(1).ToString("yyyy-MM-dd")
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, mealPlan.Id, context);
        
        // Assert - function returns OK when adding recipe successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify saved to database
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var savedAssignment = await verifyDb.Set<MealPlanRecipe>()
            .FirstOrDefaultAsync(mpr => mpr.MealPlanId == mealPlan.Id && mpr.RecipeId == TestData.Recipe1Id);
        
        Assert.NotNull(savedAssignment);
    }
    
    [Fact]
    public async Task AddRecipeToMealPlan_NonExistentMealPlan_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<AddRecipeToMealPlan>();
        
        var function = new AddRecipeToMealPlan(logger, db, authHelper);
        
        var request = new
        {
            recipeId = TestData.Recipe1Id,
            day = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), context);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task AddRecipeToMealPlan_DayOutsideRange_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow.Date;
        var mealPlan = MealPlanBuilder.Create()
            .WithDateRange(startDate, startDate.AddDays(6))
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.MealPlans.Add(mealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<AddRecipeToMealPlan>();
        
        var function = new AddRecipeToMealPlan(logger, db, authHelper);
        
        var request = new
        {
            recipeId = TestData.Recipe1Id,
            day = startDate.AddDays(10).ToString("yyyy-MM-dd") // Outside range
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, mealPlan.Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task AddRecipeToMealPlan_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var mealPlan = MealPlanBuilder.CreateForCurrentWeek().Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.MealPlans.Add(mealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<AddRecipeToMealPlan>();
        
        var function = new AddRecipeToMealPlan(logger, db, authHelper);
        
        var request = new
        {
            recipeId = TestData.Recipe1Id,
            day = mealPlan.StartDate.ToString("yyyy-MM-dd")
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            authHeader: null);
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, mealPlan.Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    #endregion
    
    #region GetMealPlan Tests
    
    [Fact]
    public async Task GetMealPlan_ExistingPlan_ReturnsMealPlanWithRecipes()
    {
        // Arrange
        var mealPlan = MealPlanBuilder.CreateForCurrentWeek()
            .WithRecipeOnMonday(TestData.Recipe1Id)
            .WithRecipeOnWednesday(TestData.Recipe2Id)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.MealPlans.Add(mealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetMealPlan>();
        
        var function = new GetMealPlan(logger, db, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, mealPlan.Id, context);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await ReadResponseBody<JsonElement>(response);
        Assert.Equal(mealPlan.Id.ToString(), responseBody.GetProperty("id").GetString());
        // Response contains "days" array with recipes, and "recipesAssigned" count
        Assert.True(responseBody.TryGetProperty("days", out var days));
        Assert.True(days.GetArrayLength() > 0);
        Assert.Equal(2, responseBody.GetProperty("recipesAssigned").GetInt32());
    }
    
    [Fact]
    public async Task GetMealPlan_NonExistentPlan_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var blobUrlService = scope.ServiceProvider.GetRequiredService<IBlobUrlService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<GetMealPlan>();
        
        var function = new GetMealPlan(logger, db, authHelper, blobUrlService);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Get,
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        var context = new Mock<FunctionContext>().Object;
        
        // Act
        var response = await function.Run(httpRequest, Guid.NewGuid(), context);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    #endregion
    
    #region RemoveRecipeFromMealPlan Tests
    
    [Fact]
    public async Task RemoveRecipeFromMealPlan_ExistingAssignment_RemovesRecipe()
    {
        // Arrange
        var mealPlan = MealPlanBuilder.CreateForCurrentWeek()
            .WithRecipeOnMonday(TestData.Recipe1Id)
            .Build();
        
        await using (var setupDb = _fixture.TestHost.CreateDbContext())
        {
            setupDb.MealPlans.Add(mealPlan);
            await setupDb.SaveChangesAsync();
        }
        
        // Get the day of the recipe assignment
        string dayString;
        await using (var getDb = _fixture.TestHost.CreateDbContext())
        {
            var assignment = await getDb.Set<MealPlanRecipe>()
                .FirstAsync(mpr => mpr.MealPlanId == mealPlan.Id);
            dayString = assignment.Day.ToString("yyyy-MM-dd");
        }
        
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RemoveRecipeFromMealPlan>();
        
        var function = new RemoveRecipeFromMealPlan(logger, db, authHelper);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Delete,
            authHeader: TestUsers.User1.CreateAuthHeader().ToString());
        
        // Act
        var response = await function.RunAsync(httpRequest, mealPlan.Id.ToString(), dayString);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify removed from database
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var remainingAssignment = await verifyDb.Set<MealPlanRecipe>()
            .FirstOrDefaultAsync(mpr => mpr.MealPlanId == mealPlan.Id);
        Assert.Null(remainingAssignment);
    }
    
    #endregion
    
    #region Helper Methods
    
    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method, 
        string? body = null, 
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/mealplans", body, authHeader);
    }
    
    private static async Task<T?> ReadResponseBody<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }
    
    #endregion
}
