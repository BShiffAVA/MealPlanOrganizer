using FluentAssertions;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Unit.Services;

/// <summary>
/// Unit tests for RecipeRecommendationService.
/// Tests the smart recipe scoring algorithm (30% rating, 40% frequency preference, 30% recency penalty).
/// </summary>
public class RecipeRecommendationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RecipeRecommendationService _service;
    private readonly Mock<ILogger<RecipeRecommendationService>> _loggerMock;
    private readonly DateTime _weekStartDate = new(2026, 2, 9); // Sunday, Feb 9, 2026

    public RecipeRecommendationServiceTests()
    {
        // Use in-memory database for unit tests
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<RecipeRecommendationService>>();
        _service = new RecipeRecommendationService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private Recipe CreateRecipe(string title = "Test Recipe")
    {
        return new Recipe
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private RecipeRating CreateRating(Guid recipeId, int rating, string userId = "user1", string? frequencyPreference = null)
    {
        return new RecipeRating
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            UserId = userId,
            Rating = rating,
            FrequencyPreference = frequencyPreference,
            RatedUtc = DateTime.UtcNow
        };
    }

    private MealPlanRecipe CreateMealPlanRecipe(Guid recipeId, DateTime day)
    {
        var mealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            StartDate = day,
            EndDate = day.AddDays(6),
            CreatedUtc = DateTime.UtcNow
        };
        _context.MealPlans.Add(mealPlan);

        return new MealPlanRecipe
        {
            Id = Guid.NewGuid(),
            MealPlanId = mealPlan.Id,
            RecipeId = recipeId,
            Day = day,
            CreatedUtc = DateTime.UtcNow
        };
    }

    #endregion

    #region ScoreRecipe_HighRating_ReturnsHighScore

    [Fact]
    public async Task ScoreRecipe_HighRating_ReturnsHighScore()
    {
        // Arrange: Recipe with 5-star average rating
        var recipe = CreateRecipe("Spaghetti Carbonara");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 5, "user1"),
            CreateRating(recipe.Id, 5, "user2"),
            CreateRating(recipe.Id, 5, "user3"),
            CreateRating(recipe.Id, 5, "user4")
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: A 5-star recipe should get maximum rating points (30 out of 30)
        var result = recommendations.Single();
        result.AverageRating.Should().Be(5.0);
        result.ReasonCodes.Should().Contain("HighlyRated");
        // With 5-star rating (30 pts) + neutral frequency (20 pts) + never cooked (30 pts) = 80 pts
        result.Score.Should().BeApproximately(80.0, 1.0);
    }

    [Theory]
    [InlineData(1, 0)]    // 1 star = 0% of rating weight
    [InlineData(2, 7.5)]  // 2 stars = 25% of 30 = 7.5 pts
    [InlineData(3, 15)]   // 3 stars = 50% of 30 = 15 pts
    [InlineData(4, 22.5)] // 4 stars = 75% of 30 = 22.5 pts
    [InlineData(5, 30)]   // 5 stars = 100% of 30 = 30 pts
    public async Task ScoreRecipe_VariousRatings_ScalesLinearly(int rating, double expectedRatingPoints)
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Ratings = new List<RecipeRating> { CreateRating(recipe.Id, rating) };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Rating score + 20 (neutral frequency) + 30 (never cooked) = expectedRatingPoints + 50
        var result = recommendations.Single();
        result.AverageRating.Should().Be(rating);
        result.Score.Should().BeApproximately(expectedRatingPoints + 50, 0.5);
    }

    #endregion

    #region ScoreRecipe_FrequencyOnceAWeek_BoostsScore

    [Fact]
    public async Task ScoreRecipe_FrequencyOnceAWeek_BoostsScore()
    {
        // Arrange: Recipe with "OnceAWeek" frequency, last cooked 8 days ago (past ideal)
        var recipe = CreateRecipe("Taco Night");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "user1", "OnceAWeek"),
            CreateRating(recipe.Id, 4, "user2", "OnceAWeek")
        };
        _context.Recipes.Add(recipe);

        // Add meal plan entry 8 days ago
        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-8));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get full frequency points (40) since 8 days >= 7 days ideal
        var result = recommendations.Single();
        result.FrequencyPreference.Should().Be("OnceAWeek");
        result.ReasonCodes.Should().Contain("MeetsFrequency");
    }

    [Theory]
    [InlineData("OnceAWeek", 7)]
    [InlineData("OnceAMonth", 30)]
    [InlineData("AFewTimesAYear", 90)]
    [InlineData("Yearly", 365)]
    public async Task ScoreRecipe_FrequencyMet_GetsFullFrequencyPoints(string frequency, int daysAgo)
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "user1", frequency)
        };
        _context.Recipes.Add(recipe);

        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-daysAgo));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert
        var result = recommendations.Single();
        result.ReasonCodes.Should().Contain("MeetsFrequency");
    }

    [Fact]
    public async Task ScoreRecipe_FrequencyNotMet_GetsPartialFrequencyPoints()
    {
        // Arrange: "OnceAWeek" recipe cooked only 3 days ago (< 7 day ideal)
        var recipe = CreateRecipe();
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "user1", "OnceAWeek")
        };
        _context.Recipes.Add(recipe);

        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-3));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get partial frequency points (3/7 * 40 = ~17 pts)
        var result = recommendations.Single();
        result.ReasonCodes.Should().NotContain("MeetsFrequency");
    }

    #endregion

    #region ScoreRecipe_RecentlyUsed_AppliesPenalty

    [Fact]
    public async Task ScoreRecipe_RecentlyUsed_AppliesPenalty()
    {
        // Arrange: Recipe cooked yesterday
        var recipe = CreateRecipe("Yesterday's Dinner");
        recipe.Ratings = new List<RecipeRating> { CreateRating(recipe.Id, 5) };
        _context.Recipes.Add(recipe);

        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-1));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get heavy recency penalty (only 10% of 30 = 3 pts for recency)
        var result = recommendations.Single();
        result.LastCookedDate.Should().NotBeNull();
        result.ReasonCodes.Should().NotContain("NotCookedRecently");
        // 30 (rating) + 20 (neutral freq) + 3 (recency penalty) = 53
        result.Score.Should().BeLessThanOrEqualTo(55);
    }

    [Theory]
    [InlineData(1, 3)]    // 1 day ago: 10% of 30 = 3 pts
    [InlineData(7, 3)]    // 7 days ago: still 10% (within week)
    [InlineData(10, 15)]  // 10 days ago: 50% of 30 = 15 pts
    [InlineData(14, 15)]  // 14 days ago: still 50% (within 2 weeks)
    [InlineData(21, 22.5)] // 21 days ago: 75% of 30 = 22.5 pts
    [InlineData(30, 22.5)] // 30 days ago: still 75% (within month)
    [InlineData(45, 30)]  // 45 days ago: 100% of 30 = 30 pts
    public async Task ScoreRecipe_RecencyPenalty_AppliesCorrectTier(int daysAgo, double expectedRecencyPoints)
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Ratings = new List<RecipeRating> { CreateRating(recipe.Id, 5) }; // 30 pts for rating
        _context.Recipes.Add(recipe);

        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-daysAgo));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: 30 (rating) + 20 (neutral freq) + expectedRecencyPoints
        var result = recommendations.Single();
        result.Score.Should().BeApproximately(30 + 20 + expectedRecencyPoints, 1.0);
    }

    #endregion

    #region ScoreRecipe_NoRatings_UsesDefaultScore

    [Fact]
    public async Task ScoreRecipe_NoRatings_UsesDefaultScore()
    {
        // Arrange: Recipe with no ratings
        var recipe = CreateRecipe("Unrated Recipe");
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Neutral rating score (50% of 30 = 15 pts) + neutral frequency (20 pts) + never cooked (30 pts) = 65 pts
        var result = recommendations.Single();
        result.AverageRating.Should().Be(0);
        result.RatingCount.Should().Be(0);
        result.ReasonCodes.Should().Contain("NeverRated");
        result.Score.Should().BeApproximately(65, 1.0);
    }

    #endregion

    #region ScoreRecipe_MultipleUserRatings_AveragesCorrectly

    [Fact]
    public async Task ScoreRecipe_MultipleUserRatings_AveragesCorrectly()
    {
        // Arrange: 4 family members with different ratings
        var recipe = CreateRecipe("Family Favorite");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 5, "adult1"),   // Adult 1: 5 stars
            CreateRating(recipe.Id, 4, "adult2"),   // Adult 2: 4 stars
            CreateRating(recipe.Id, 5, "teen1"),    // Teen 1: 5 stars
            CreateRating(recipe.Id, 3, "teen2")     // Teen 2: 3 stars
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Average = (5+4+5+3)/4 = 4.25, rounded to 4.3
        var result = recommendations.Single();
        result.AverageRating.Should().BeApproximately(4.25, 0.1);
        result.RatingCount.Should().Be(4);
        result.ReasonCodes.Should().Contain("HighlyRated"); // >= 4.0
    }

    [Fact]
    public async Task ScoreRecipe_DifferentFrequencyPreferences_UsesMostCommon()
    {
        // Arrange: Family members with different frequency preferences
        var recipe = CreateRecipe("Divisive Dish");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "adult1", "OnceAWeek"),
            CreateRating(recipe.Id, 4, "adult2", "OnceAWeek"),
            CreateRating(recipe.Id, 4, "teen1", "OnceAMonth"),
            CreateRating(recipe.Id, 4, "teen2", "OnceAWeek")
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Most common frequency is "OnceAWeek" (3 out of 4)
        var result = recommendations.Single();
        result.FrequencyPreference.Should().Be("OnceAWeek");
    }

    #endregion

    #region GetRecommendations_EmptyDatabase_ReturnsEmptyList

    [Fact]
    public async Task GetRecommendations_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange: Empty database (no recipes)

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().BeEmpty();
    }

    #endregion

    #region GetRecommendations_ExcludesRecentlyUsed (NeverMarked scenario)

    [Fact]
    public async Task GetRecommendations_MarkedNever_GetsZeroScore()
    {
        // Arrange: Recipe marked "Never" by all family members
        var recipe = CreateRecipe("Disliked Dish");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 1, "user1", "Never"),
            CreateRating(recipe.Id, 1, "user2", "Never")
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get zero score due to "Never" preference
        var result = recommendations.Single();
        result.FrequencyPreference.Should().Be("Never");
        result.Score.Should().Be(0);
        result.ReasonCodes.Should().Contain("MarkedNever");
    }

    #endregion

    #region GetRecommendations_RespectsMealPlanDuration

    [Fact]
    public async Task GetRecommendations_RespectsMealPlanDuration_ScoresCorrectly()
    {
        // Arrange: Two recipes - one cooked recently, one not
        var recentRecipe = CreateRecipe("Recent Recipe");
        recentRecipe.Ratings = new List<RecipeRating> { CreateRating(recentRecipe.Id, 5) };

        var oldRecipe = CreateRecipe("Old Recipe");
        oldRecipe.Ratings = new List<RecipeRating> { CreateRating(oldRecipe.Id, 5) };

        _context.Recipes.AddRange(recentRecipe, oldRecipe);

        // Recent recipe cooked 3 days ago
        var recentMealPlan = CreateMealPlanRecipe(recentRecipe.Id, _weekStartDate.AddDays(-3));
        _context.MealPlanRecipes.Add(recentMealPlan);

        // Old recipe cooked 60 days ago
        var oldMealPlan = CreateMealPlanRecipe(oldRecipe.Id, _weekStartDate.AddDays(-60));
        _context.MealPlanRecipes.Add(oldMealPlan);

        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Old recipe should score higher due to less recency penalty
        recommendations.Should().HaveCount(2);
        var sorted = recommendations.OrderByDescending(r => r.Score).ToList();
        sorted[0].Title.Should().Be("Old Recipe");
        sorted[1].Title.Should().Be("Recent Recipe");
        sorted[0].Score.Should().BeGreaterThan(sorted[1].Score);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task GetRecommendations_SortsByScoreDescending()
    {
        // Arrange: Multiple recipes with different ratings
        var lowRated = CreateRecipe("Low Rated");
        lowRated.Ratings = new List<RecipeRating> { CreateRating(lowRated.Id, 2) };

        var mediumRated = CreateRecipe("Medium Rated");
        mediumRated.Ratings = new List<RecipeRating> { CreateRating(mediumRated.Id, 3) };

        var highRated = CreateRecipe("High Rated");
        highRated.Ratings = new List<RecipeRating> { CreateRating(highRated.Id, 5) };

        _context.Recipes.AddRange(lowRated, mediumRated, highRated);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should be sorted by score descending
        recommendations.Should().HaveCount(3);
        recommendations[0].Title.Should().Be("High Rated");
        recommendations[1].Title.Should().Be("Medium Rated");
        recommendations[2].Title.Should().Be("Low Rated");
    }

    [Fact]
    public async Task GetRecommendations_TieBreaksByAverageRating()
    {
        // Arrange: Two recipes with same overall score but different ratings
        var recipe1 = CreateRecipe("Recipe A");
        recipe1.Ratings = new List<RecipeRating> { CreateRating(recipe1.Id, 5) };

        var recipe2 = CreateRecipe("Recipe B");
        recipe2.Ratings = new List<RecipeRating> { CreateRating(recipe2.Id, 4) };

        // Make overall scores equal by adjusting recency
        // Recipe A: 5 stars, never cooked
        // Recipe B: 4 stars, but cooked 45+ days ago (full recency points)
        _context.Recipes.AddRange(recipe1, recipe2);

        // Don't add meal plan for recipe1 (never cooked)
        // Add old meal plan for recipe2 to get full recency points
        var oldMealPlan = CreateMealPlanRecipe(recipe2.Id, _weekStartDate.AddDays(-100));
        _context.MealPlanRecipes.Add(oldMealPlan);

        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Both have similar scores, but Recipe A should win on rating tiebreaker
        recommendations.Should().HaveCount(2);
        recommendations[0].Title.Should().Be("Recipe A");
        recommendations[0].AverageRating.Should().BeGreaterThan(recommendations[1].AverageRating);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetRecommendations_NullFrequencyPreference_GetsNeutralScore()
    {
        // Arrange: Recipe with rating but no frequency preference
        var recipe = CreateRecipe("No Frequency Set");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "user1", null)
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get neutral frequency score (50% of 40 = 20 pts)
        var result = recommendations.Single();
        result.FrequencyPreference.Should().BeNull();
        // 22.5 (4-star rating) + 20 (neutral freq) + 30 (never cooked) = 72.5
        result.Score.Should().BeApproximately(72.5, 1.0);
    }

    [Fact]
    public async Task GetRecommendations_RecipeWithAllFields_ReturnsCompleteData()
    {
        // Arrange: Recipe with all optional fields populated
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Complete Recipe",
            CuisineType = "Italian",
            PrepTimeMinutes = 15,
            CookTimeMinutes = 30,
            ImageUrl = "https://example.com/image.jpg",
            CreatedUtc = DateTime.UtcNow
        };
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 5, "user1", "OnceAWeek")
        };
        _context.Recipes.Add(recipe);

        var mealPlan = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-10));
        _context.MealPlanRecipes.Add(mealPlan);

        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: All fields should be populated
        var result = recommendations.Single();
        result.RecipeId.Should().Be(recipe.Id);
        result.Title.Should().Be("Complete Recipe");
        result.CuisineType.Should().Be("Italian");
        result.PrepTimeMinutes.Should().Be(15);
        result.CookTimeMinutes.Should().Be(30);
        result.ImageUrl.Should().Be("https://example.com/image.jpg");
        result.LastCookedDate.Should().NotBeNull();
        result.FrequencyPreference.Should().Be("OnceAWeek");
        result.AverageRating.Should().Be(5);
        result.RatingCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRecommendations_NeverCookedRecipe_GetsFullRecencyPoints()
    {
        // Arrange: Recipe that has never been cooked
        var recipe = CreateRecipe("Never Cooked");
        recipe.Ratings = new List<RecipeRating> { CreateRating(recipe.Id, 4) };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert
        var result = recommendations.Single();
        result.LastCookedDate.Should().BeNull();
        result.ReasonCodes.Should().Contain("NeverCooked");
        // Should get full recency points (30)
        // 22.5 (4-star) + 20 (neutral freq) + 30 (never cooked) = 72.5
        result.Score.Should().BeApproximately(72.5, 1.0);
    }

    #endregion
}
