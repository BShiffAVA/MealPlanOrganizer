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
        // Only allow valid NextTimePreference values
        string? nextTimePreference = frequencyPreference switch
        {
            "RightAway" or "In2Weeks" or "NextMonth" or "NextYear" or "Never" => frequencyPreference,
            _ => null
        };
        return new RecipeRating
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            UserId = userId,
            Rating = rating,
            NextTimePreference = nextTimePreference,
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
        // Arrange: Recipe with "RightAway" frequency, last cooked 8 days ago (past ideal)
        var recipe = CreateRecipe("Taco Night");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "user1", "RightAway"),
            CreateRating(recipe.Id, 4, "user2", "RightAway")
        };
        _context.Recipes.Add(recipe);

        // Add meal plan entry 8 days ago
        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-8));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get full frequency points (40) since 8 days >= 0 days ideal (RightAway)
        var result = recommendations.Single();
        result.NextTimePreference.Should().Be("RightAway");
        result.ReasonCodes.Should().Contain("MeetsFrequency");
    }

    [Theory]
    [InlineData("RightAway", 0)]
    [InlineData("NextMonth", 30)]
    [InlineData("NextYear", 365)]
    public async Task ScoreRecipe_FrequencyMet_GetsFullFrequencyPoints(string frequency, int daysAgo)
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Ratings = new List<RecipeRating> {
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
        // Arrange: "RightAway" recipe cooked only 3 days ago (< 0 day ideal)
        var recipe = CreateRecipe();
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "user1", "RightAway")
        };
        _context.Recipes.Add(recipe);

        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-3));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Should get full frequency points for RightAway (0 days ideal, any daysSinceCooked >= 0)
        var result = recommendations.Single();
        result.ReasonCodes.Should().Contain("MeetsFrequency");
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
        // Arrange: Family members with different next time preferences
        var recipe = CreateRecipe("Divisive Dish");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, "adult1", "RightAway"),
            CreateRating(recipe.Id, 4, "adult2", "RightAway"),
            CreateRating(recipe.Id, 4, "teen1", "NextMonth"),
            CreateRating(recipe.Id, 4, "teen2", "RightAway")
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);

        // Assert: Weighted average is 7.5 days (RightAway=0, NextMonth=30, so (0+0+30+0)/4=7.5), which maps to "In2Weeks"
        var result = recommendations.Single();
        result.NextTimePreference.Should().Be("In2Weeks");
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
        result.NextTimePreference.Should().Be("Never");
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
        result.NextTimePreference.Should().BeNull();
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
            CreateRating(recipe.Id, 5, "user1", "RightAway")
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
        result.NextTimePreference.Should().Be("RightAway");
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

    #region WeightedAverageCalculation

    /// <summary>
    /// Tests that ratings are calculated using weighted average based on HouseholdMember.Weight.
    /// Example: User1 (Weight 5) rates 1, User2 (Weight 1) rates 5
    /// Weighted average = (1×5 + 5×1) / (5+1) = 10/6 ≈ 1.67 (rounded to 1.7)
    /// </summary>
    [Fact]
    public async Task AverageRating_WithMemberWeights_CalculatesWeightedAverage()
    {
        // Arrange: Create two users with different weights
        var user1ExternalId = "user1-external-id";
        var user2ExternalId = "user2-external-id";
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Test Household",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(household);
        
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user1ExternalId,
            Email = "user1@example.com",
            DisplayName = "User One",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user1);
        
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user2ExternalId,
            Email = "user2@example.com",
            DisplayName = "User Two",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user2);
        
        // User1 has weight 5, User2 has weight 1
        var membership1 = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            HouseholdId = household.Id,
            Weight = 5,
            JoinedUtc = DateTime.UtcNow
        };
        _context.HouseholdMembers.Add(membership1);
        
        var membership2 = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            HouseholdId = household.Id,
            Weight = 1,
            JoinedUtc = DateTime.UtcNow
        };
        _context.HouseholdMembers.Add(membership2);
        
        // Create recipe with ratings: User1 rates 1, User2 rates 5
        var recipe = CreateRecipe("Weighted Test Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 1, user1ExternalId),  // Weight 5
            CreateRating(recipe.Id, 5, user2ExternalId)   // Weight 1
        };
        _context.Recipes.Add(recipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert
        // Weighted average = (1×5 + 5×1) / (5+1) = 10/6 ≈ 1.67, rounded to 1.7
        var result = recommendations.Single();
        result.AverageRating.Should().BeApproximately(1.7, 0.1);
    }

    [Fact]
    public async Task AverageRating_WithEqualWeights_MatchesSimpleAverage()
    {
        // Arrange: Create users with equal weights (default 3)
        var user1ExternalId = "equal-weight-user1";
        var user2ExternalId = "equal-weight-user2";
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Equal Weight Household",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(household);
        
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user1ExternalId,
            Email = "eq1@example.com",
            DisplayName = "Equal User One",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user1);
        
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user2ExternalId,
            Email = "eq2@example.com",
            DisplayName = "Equal User Two",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user2);
        
        // Both users have weight 3 (default)
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            HouseholdId = household.Id,
            Weight = 3,
            JoinedUtc = DateTime.UtcNow
        });
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            HouseholdId = household.Id,
            Weight = 3,
            JoinedUtc = DateTime.UtcNow
        });
        
        // Create recipe with ratings: 2 and 4
        var recipe = CreateRecipe("Equal Weight Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 2, user1ExternalId),
            CreateRating(recipe.Id, 4, user2ExternalId)
        };
        _context.Recipes.Add(recipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert: (2×3 + 4×3) / (3+3) = 18/6 = 3.0 = simple average
        var result = recommendations.Single();
        result.AverageRating.Should().Be(3.0);
    }

    [Fact]
    public async Task AverageRating_WithUnknownUsers_UsesDefaultWeight()
    {
        // Arrange: Create recipe with ratings from users not in any household
        // They should get default weight of 3
        var recipe = CreateRecipe("Unknown Users Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 2, "unknown-user-1"),
            CreateRating(recipe.Id, 4, "unknown-user-2")
        };
        _context.Recipes.Add(recipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert: (2×3 + 4×3) / (3+3) = 3.0 since both use default weight 3
        var result = recommendations.Single();
        result.AverageRating.Should().Be(3.0);
    }

    [Fact]
    public async Task AverageRating_HighWeight5LowRating_LowersAverage()
    {
        // Arrange: User with max weight gives lowest rating
        var heavyUserExternalId = "heavy-user";
        var lightUserExternalId = "light-user";
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Weight Distribution Test",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(household);
        
        var heavyUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = heavyUserExternalId,
            Email = "heavy@example.com",
            DisplayName = "Heavy Weight User",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(heavyUser);
        
        var lightUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = lightUserExternalId,
            Email = "light@example.com",
            DisplayName = "Light Weight User",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(lightUser);
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = heavyUser.Id,
            HouseholdId = household.Id,
            Weight = 5,
            JoinedUtc = DateTime.UtcNow
        });
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = lightUser.Id,
            HouseholdId = household.Id,
            Weight = 1,
            JoinedUtc = DateTime.UtcNow
        });
        
        // Heavy weight user gives 1 star, light weight gives 5 stars
        var recipe = CreateRecipe("Heavy Low Rating Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 1, heavyUserExternalId),  // Weight 5 × Rating 1 = 5
            CreateRating(recipe.Id, 5, lightUserExternalId)    // Weight 1 × Rating 5 = 5
        };
        _context.Recipes.Add(recipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert: (1×5 + 5×1) / (5+1) = 10/6 ≈ 1.67
        // This is significantly lower than simple average of (1+5)/2 = 3.0
        var result = recommendations.Single();
        result.AverageRating.Should().BeLessThan(2.0);
        result.AverageRating.Should().BeApproximately(1.7, 0.1);
    }

    #endregion

    #region WeightedFrequencyCalculation

    /// <summary>
    /// Tests that frequency preference is calculated using weighted average based on HouseholdMember.Weight.
    /// Example: User1 (Weight 5) prefers OnceAWeek (7 days), User2 (Weight 1) prefers OnceAMonth (30 days)
    /// Weighted average = (7×5 + 30×1) / (5+1) = 65/6 ≈ 11 days (rounds to OnceAMonth threshold of 15+)
    /// </summary>
    [Fact]
    public async Task FrequencyPreference_WithMemberWeights_CalculatesWeightedAverageDays()
    {
        // Arrange: Create two users with different weights and frequency preferences
        var user1ExternalId = "freq-user1";
        var user2ExternalId = "freq-user2";
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Frequency Test Household",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(household);
        
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user1ExternalId,
            Email = "freq1@example.com",
            DisplayName = "Freq User One",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user1);
        
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user2ExternalId,
            Email = "freq2@example.com",
            DisplayName = "Freq User Two",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user2);
        
        // User1 has weight 5, User2 has weight 1
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            HouseholdId = household.Id,
            Weight = 5,
            JoinedUtc = DateTime.UtcNow
        });
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            HouseholdId = household.Id,
            Weight = 1,
            JoinedUtc = DateTime.UtcNow
        });
        
        // User1 (weight 5) prefers RightAway (0 days)
        // User2 (weight 1) prefers NextMonth (30 days)
        // Weighted average = (0×5 + 30×1) / 6 = 30/6 = 5 days → RightAway (< 7)
        var recipe = CreateRecipe("Weighted Frequency Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, user1ExternalId, "RightAway"),
            CreateRating(recipe.Id, 4, user2ExternalId, "NextMonth")
        };
        _context.Recipes.Add(recipe);
        
        // Last cooked 11 days ago - should meet weighted frequency of ~11 days
        var mealPlanRecipe = CreateMealPlanRecipe(recipe.Id, _weekStartDate.AddDays(-11));
        _context.MealPlanRecipes.Add(mealPlanRecipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert: Weighted average 5 days is displayed as RightAway
        var result = recommendations.Single();
        result.NextTimePreference.Should().Be("RightAway");
        result.ReasonCodes.Should().Contain("MeetsFrequency");
    }

    [Fact]
    public async Task FrequencyPreference_HighWeightNever_ResultsInLowScore()
    {
        // Arrange: Heavy weight user says "Never" while light weight says "OnceAWeek"
        var heavyUserExternalId = "never-heavy-user";
        var lightUserExternalId = "never-light-user";
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Never Test Household",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(household);
        
        var heavyUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = heavyUserExternalId,
            Email = "never-heavy@example.com",
            DisplayName = "Never Heavy User",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(heavyUser);
        
        var lightUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = lightUserExternalId,
            Email = "never-light@example.com",
            DisplayName = "Never Light User",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(lightUser);
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = heavyUser.Id,
            HouseholdId = household.Id,
            Weight = 5,
            JoinedUtc = DateTime.UtcNow
        });
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = lightUser.Id,
            HouseholdId = household.Id,
            Weight = 1,
            JoinedUtc = DateTime.UtcNow
        });
        
        // Heavy weight (5) says Never (5000 days), Light weight (1) says OnceAWeek (7 days)
        // Weighted average = (5000×5 + 7×1) / 6 = 25007/6 ≈ 4168 days → "Never" (>= 2500)
        var recipe = CreateRecipe("Never Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, heavyUserExternalId, "Never"),
            CreateRating(recipe.Id, 5, lightUserExternalId, "OnceAWeek")
        };
        _context.Recipes.Add(recipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert: Should be marked as "Never" and get 0 score
        var result = recommendations.Single();
        result.NextTimePreference.Should().Be("Never");
        result.Score.Should().Be(0);
        result.ReasonCodes.Should().Contain("MarkedNever");
    }

    [Fact]
    public async Task FrequencyPreference_EqualWeightsDifferentFrequencies_CalculatesSimpleAverage()
    {
        // Arrange: Equal weights with different frequencies
        var user1ExternalId = "equal-freq-user1";
        var user2ExternalId = "equal-freq-user2";
        
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Equal Frequency Household",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(household);
        
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user1ExternalId,
            Email = "eqfreq1@example.com",
            DisplayName = "EqFreq User One",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user1);
        
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = user2ExternalId,
            Email = "eqfreq2@example.com",
            DisplayName = "EqFreq User Two",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Users.Add(user2);
        
        // Both have weight 3 (default)
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            HouseholdId = household.Id,
            Weight = 3,
            JoinedUtc = DateTime.UtcNow
        });
        
        _context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            HouseholdId = household.Id,
            Weight = 3,
            JoinedUtc = DateTime.UtcNow
        });
        
        // User1 prefers RightAway (0 days), User2 prefers NextYear (365 days)
        // Simple average = (0×3 + 365×3) / 6 = 1095/6 ≈ 182 days → NextYear (>= 300)
        var recipe = CreateRecipe("Equal Freq Weight Recipe");
        recipe.Ratings = new List<RecipeRating>
        {
            CreateRating(recipe.Id, 4, user1ExternalId, "RightAway"),
            CreateRating(recipe.Id, 4, user2ExternalId, "NextYear")
        };
        _context.Recipes.Add(recipe);
        
        await _context.SaveChangesAsync();
        
        // Act
        var recommendations = await _service.GetRecommendedRecipesAsync(_weekStartDate);
        
        // Assert: Average 182 days is displayed as NextMonth (mapping: 22–299 days)
        var result = recommendations.Single();
        result.NextTimePreference.Should().Be("NextMonth");
    }

    #endregion
}
