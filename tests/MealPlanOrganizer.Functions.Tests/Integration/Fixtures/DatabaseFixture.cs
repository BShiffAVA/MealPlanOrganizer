using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

/// <summary>
/// xUnit collection fixture that manages a SQLite in-memory database for integration tests.
/// No Docker required - uses SQLite's in-memory mode with a persistent connection.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    
    public string ConnectionString => _connection?.ConnectionString ?? string.Empty;
    
    public async Task InitializeAsync()
    {
        // Create a shared in-memory SQLite connection
        // Mode=Memory;Cache=Shared allows multiple contexts to share the same database
        _connection = new SqliteConnection("Data Source=IntegrationTests;Mode=Memory;Cache=Shared");
        await _connection.OpenAsync();
        
        // Create schema using EnsureCreated (migrations don't work well with SQLite)
        // First delete any existing database to avoid "table already exists" errors
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // Seed test data
        await SeedTestDataAsync(context);
    }
    
    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
    
    /// <summary>
    /// Creates a new DbContext instance connected to the in-memory SQLite database.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        
        return new AppDbContext(options);
    }
    
    /// <summary>
    /// Creates a fresh DbContext with an isolated database for tests that need complete isolation.
    /// </summary>
    public async Task<AppDbContext> CreateIsolatedDbContextAsync()
    {
        var uniqueDbName = $"Isolated_{Guid.NewGuid():N}";
        var isolatedConnection = new SqliteConnection($"Data Source={uniqueDbName};Mode=Memory;Cache=Shared");
        await isolatedConnection.OpenAsync();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(isolatedConnection)
            .Options;
        
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
    
    /// <summary>
    /// Resets the database to initial test state by clearing all data and re-seeding.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateDbContext();
        
        // Clear all data in correct order to respect foreign keys
        context.Set<MealPlanRecipe>().RemoveRange(context.Set<MealPlanRecipe>());
        context.MealPlans.RemoveRange(context.MealPlans);
        context.RecipeRatings.RemoveRange(context.RecipeRatings);
        context.Set<RecipeIngredient>().RemoveRange(context.Set<RecipeIngredient>());
        context.Set<RecipeStep>().RemoveRange(context.Set<RecipeStep>());
        context.Recipes.RemoveRange(context.Recipes);
        
        // Clear invite codes before clearing users (FK constraint)
        context.InviteCodes.RemoveRange(context.InviteCodes);
        
        // Clear user/household data
        context.HouseholdMembers.RemoveRange(context.HouseholdMembers);
        context.Households.RemoveRange(context.Households);
        context.Users.RemoveRange(context.Users);
        
        await context.SaveChangesAsync();
        
        // Re-seed
        await SeedTestDataAsync(context);
    }
    
    /// <summary>
    /// Seeds the database with standard test data.
    /// </summary>
    public async Task SeedTestDataAsync(AppDbContext context)
    {
        // Seed test users first (needed for Recipe.CreatedByUserId FK)
        var users = new List<User>
        {
            new()
            {
                Id = TestData.User1InternalId,
                ExternalIdObjectId = TestData.User1Id,
                Email = TestData.User1Email,
                DisplayName = TestData.User1DisplayName
            },
            new()
            {
                Id = TestData.User2InternalId,
                ExternalIdObjectId = TestData.User2Id,
                Email = TestData.User2Email,
                DisplayName = TestData.User2DisplayName
            }
        };
        
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        
        // Seed test recipes
        var recipes = new List<Recipe>
        {
            new()
            {
                Id = TestData.Recipe1Id,
                Title = "Test Spaghetti Carbonara",
                Description = "Classic Italian pasta dish",
                CuisineType = "Italian",
                PrepTimeMinutes = 15,
                CookTimeMinutes = 20,
                Servings = 4,
                CreatedBy = TestData.User1DisplayName,
                CreatedByUserId = TestData.User1InternalId,
                Ingredients = new List<RecipeIngredient>
                {
                    new() { Id = Guid.NewGuid(), Name = "Spaghetti", Quantity = "400", QuantityValue = 400, Unit = "g" },
                    new() { Id = Guid.NewGuid(), Name = "Eggs", Quantity = "4", QuantityValue = 4, Unit = "whole" },
                    new() { Id = Guid.NewGuid(), Name = "Pecorino Romano", Quantity = "100", QuantityValue = 100, Unit = "g" },
                    new() { Id = Guid.NewGuid(), Name = "Guanciale", Quantity = "200", QuantityValue = 200, Unit = "g" }
                },
                Steps = new List<RecipeStep>
                {
                    new() { Id = Guid.NewGuid(), StepNumber = 1, Instruction = "Cook pasta in salted boiling water" },
                    new() { Id = Guid.NewGuid(), StepNumber = 2, Instruction = "Fry guanciale until crispy" },
                    new() { Id = Guid.NewGuid(), StepNumber = 3, Instruction = "Mix eggs with cheese" },
                    new() { Id = Guid.NewGuid(), StepNumber = 4, Instruction = "Combine all and serve immediately" }
                }
            },
            new()
            {
                Id = TestData.Recipe2Id,
                Title = "Test Chicken Tikka Masala",
                Description = "Creamy Indian curry",
                CuisineType = "Indian",
                PrepTimeMinutes = 30,
                CookTimeMinutes = 45,
                Servings = 6,
                CreatedBy = TestData.User1DisplayName,
                CreatedByUserId = TestData.User1InternalId,
                Ingredients = new List<RecipeIngredient>
                {
                    new() { Id = Guid.NewGuid(), Name = "Chicken Breast", Quantity = "800", QuantityValue = 800, Unit = "g" },
                    new() { Id = Guid.NewGuid(), Name = "Yogurt", Quantity = "200", QuantityValue = 200, Unit = "ml" },
                    new() { Id = Guid.NewGuid(), Name = "Tikka Masala Paste", Quantity = "4", QuantityValue = 4, Unit = "tbsp" }
                },
                Steps = new List<RecipeStep>
                {
                    new() { Id = Guid.NewGuid(), StepNumber = 1, Instruction = "Marinate chicken in yogurt and spices" },
                    new() { Id = Guid.NewGuid(), StepNumber = 2, Instruction = "Grill or bake the chicken" },
                    new() { Id = Guid.NewGuid(), StepNumber = 3, Instruction = "Prepare the masala sauce" },
                    new() { Id = Guid.NewGuid(), StepNumber = 4, Instruction = "Combine chicken with sauce and simmer" }
                }
            },
            new()
            {
                Id = TestData.Recipe3Id,
                Title = "Test Simple Salad",
                Description = "Quick healthy salad",
                CuisineType = "American",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 0,
                Servings = 2,
                CreatedBy = TestData.User2DisplayName,
                CreatedByUserId = TestData.User2InternalId
            }
        };
        
        context.Recipes.AddRange(recipes);
        
        // Seed test ratings
        var ratings = new List<RecipeRating>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RecipeId = TestData.Recipe1Id,
                UserId = TestData.User1Id,
                Rating = 5,
                Comments = "Love this recipe!",
                FrequencyPreference = "OnceAWeek"
            },
            new()
            {
                Id = Guid.NewGuid(),
                RecipeId = TestData.Recipe1Id,
                UserId = TestData.User2Id,
                Rating = 4,
                Comments = "Very good!"
            },
            new()
            {
                Id = Guid.NewGuid(),
                RecipeId = TestData.Recipe2Id,
                UserId = TestData.User1Id,
                Rating = 5,
                Comments = "Amazing curry!"
            }
        };
        
        context.RecipeRatings.AddRange(ratings);
        
        // Seed test meal plans
        var mealPlan = new MealPlan
        {
            Id = TestData.MealPlan1Id,
            Name = "Test Week Plan",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(6),
            CreatedBy = TestData.User1Id.ToString(),
            Status = "Draft",
            Recipes = new List<MealPlanRecipe>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RecipeId = TestData.Recipe1Id,
                    Day = DateTime.UtcNow.Date
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RecipeId = TestData.Recipe2Id,
                    Day = DateTime.UtcNow.Date.AddDays(1)
                }
            }
        };
        
        context.MealPlans.Add(mealPlan);
        
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Static test data identifiers for consistent test references.
/// </summary>
public static class TestData
{
    public static readonly Guid Recipe1Id = Guid.Parse("00000001-0000-0000-0000-000000000001");
    public static readonly Guid Recipe2Id = Guid.Parse("00000001-0000-0000-0000-000000000002");
    public static readonly Guid Recipe3Id = Guid.Parse("00000001-0000-0000-0000-000000000003");
    
    public static readonly Guid MealPlan1Id = Guid.Parse("00000002-0000-0000-0000-000000000001");
    
    // External IDs (from JWT oid claims)
    public static readonly string User1Id = "test-user-1";
    public static readonly string User2Id = "test-user-2";
    
    // Internal database User.Id GUIDs
    public static readonly Guid User1InternalId = Guid.Parse("00000003-0000-0000-0000-000000000001");
    public static readonly Guid User2InternalId = Guid.Parse("00000003-0000-0000-0000-000000000002");
    
    public static readonly string User1Email = "user1@test.com";
    public static readonly string User2Email = "user2@test.com";
    public static readonly string User1DisplayName = "Test User 1";
    public static readonly string User2DisplayName = "Test User 2";
    
    public static readonly string HouseholdId = "test-household-1";
}

/// <summary>
/// xUnit collection definition for tests sharing the database fixture.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
