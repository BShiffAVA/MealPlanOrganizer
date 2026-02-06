using MealPlanOrganizer.Functions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Service for generating smart recipe recommendations for meal planning.
/// Uses a scoring algorithm based on ratings, frequency preferences, and recency.
/// </summary>
public class RecipeRecommendationService : IRecipeRecommendationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<RecipeRecommendationService> _logger;

    // Scoring weights (must sum to 100)
    private const double RatingWeight = 30.0;      // 30% - Higher rating = higher score
    private const double FrequencyWeight = 40.0;   // 40% - Match frequency preference
    private const double RecencyWeight = 30.0;     // 30% - Penalty for recently cooked

    // Frequency preference to ideal days between cooking
    private static readonly Dictionary<string, int> FrequencyToDays = new()
    {
        { "OnceAWeek", 7 },
        { "OnceAMonth", 30 },
        { "AFewTimesAYear", 90 },
        { "Yearly", 365 },
        { "Never", int.MaxValue }
    };

    public RecipeRecommendationService(AppDbContext context, ILogger<RecipeRecommendationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<RecommendedRecipe>> GetRecommendedRecipesAsync(DateTime weekStartDate)
    {
        _logger.LogInformation("Generating recipe recommendations for week starting {WeekStart}", weekStartDate);

        // Get all recipes with their ratings
        var recipes = await _context.Recipes
            .Include(r => r.Ratings)
            .ToListAsync();

        // Get last cooked date for each recipe from meal plans
        var lastCookedDates = await _context.MealPlanRecipes
            .GroupBy(mpr => mpr.RecipeId)
            .Select(g => new { RecipeId = g.Key, LastCooked = g.Max(x => x.Day) })
            .ToDictionaryAsync(x => x.RecipeId, x => x.LastCooked);

        var recommendations = new List<RecommendedRecipe>();

        foreach (var recipe in recipes)
        {
            var result = new RecommendedRecipe
            {
                RecipeId = recipe.Id,
                Title = recipe.Title,
                ImageUrl = recipe.ImageUrl,
                CuisineType = recipe.CuisineType,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                CookTimeMinutes = recipe.CookTimeMinutes,
                RatingCount = recipe.Ratings?.Count ?? 0
            };

            // Calculate average rating
            if (recipe.Ratings != null && recipe.Ratings.Count > 0)
            {
                result.AverageRating = Math.Round(recipe.Ratings.Average(r => r.Rating), 1);
            }

            // Get last cooked date
            if (lastCookedDates.TryGetValue(recipe.Id, out var lastCooked))
            {
                result.LastCookedDate = lastCooked;
            }

            // Get most common frequency preference (mode)
            result.FrequencyPreference = GetMostCommonFrequency(recipe.Ratings);

            // Calculate score
            result.Score = CalculateScore(
                result.AverageRating,
                result.RatingCount,
                result.FrequencyPreference,
                result.LastCookedDate,
                weekStartDate,
                result.ReasonCodes
            );

            recommendations.Add(result);
        }

        // Sort by score descending
        var sorted = recommendations
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.AverageRating)
            .ToList();

        _logger.LogInformation("Generated {Count} recommendations", sorted.Count);
        return sorted;
    }

    private static string? GetMostCommonFrequency(ICollection<Data.Entities.RecipeRating>? ratings)
    {
        if (ratings == null || ratings.Count == 0)
            return null;

        var frequencies = ratings
            .Where(r => !string.IsNullOrEmpty(r.FrequencyPreference))
            .GroupBy(r => r.FrequencyPreference)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return frequencies?.Key;
    }

    private double CalculateScore(
        double averageRating,
        int ratingCount,
        string? frequencyPreference,
        DateTime? lastCookedDate,
        DateTime weekStartDate,
        List<string> reasonCodes)
    {
        double score = 0;

        // 1. Rating Score (0-30 points)
        // Scale 1-5 stars to 0-30 points
        if (ratingCount > 0)
        {
            var ratingScore = ((averageRating - 1) / 4.0) * RatingWeight;
            score += ratingScore;

            if (averageRating >= 4.0)
            {
                reasonCodes.Add("HighlyRated");
            }
        }
        else
        {
            // No ratings yet - give a neutral score to encourage trying
            score += RatingWeight * 0.5;
            reasonCodes.Add("NeverRated");
        }

        // 2. Frequency Fit Score (0-40 points)
        // How well does "time since last cooked" match the frequency preference?
        if (!string.IsNullOrEmpty(frequencyPreference) && frequencyPreference != "Never")
        {
            var idealDays = FrequencyToDays.GetValueOrDefault(frequencyPreference, 30);
            var daysSinceCooked = lastCookedDate.HasValue
                ? (weekStartDate - lastCookedDate.Value).Days
                : idealDays * 2; // If never cooked, treat as overdue

            // Calculate how well actual days match ideal days
            // If daysSinceCooked >= idealDays, full points
            // If daysSinceCooked < idealDays, partial points
            if (daysSinceCooked >= idealDays)
            {
                score += FrequencyWeight;
                reasonCodes.Add("MeetsFrequency");
            }
            else
            {
                // Proportional score based on how close we are
                var frequencyScore = (daysSinceCooked / (double)idealDays) * FrequencyWeight;
                score += frequencyScore;
            }
        }
        else if (frequencyPreference == "Never")
        {
            // User doesn't want this recipe - heavily penalize
            score = 0;
            reasonCodes.Add("MarkedNever");
            return score;
        }
        else
        {
            // No frequency preference - neutral score
            score += FrequencyWeight * 0.5;
        }

        // 3. Recency Score (0-30 points)
        // Penalize if cooked very recently regardless of frequency preference
        if (lastCookedDate.HasValue)
        {
            var daysSinceCooked = (weekStartDate - lastCookedDate.Value).Days;

            if (daysSinceCooked <= 7)
            {
                // Cooked in last week - heavy penalty
                score += RecencyWeight * 0.1;
            }
            else if (daysSinceCooked <= 14)
            {
                // Cooked in last 2 weeks - moderate penalty
                score += RecencyWeight * 0.5;
            }
            else if (daysSinceCooked <= 30)
            {
                // Cooked in last month - slight penalty
                score += RecencyWeight * 0.75;
            }
            else
            {
                // Not cooked recently - full points
                score += RecencyWeight;
                reasonCodes.Add("NotCookedRecently");
            }
        }
        else
        {
            // Never cooked - full recency points
            score += RecencyWeight;
            reasonCodes.Add("NeverCooked");
        }

        return Math.Round(score, 1);
    }
}
