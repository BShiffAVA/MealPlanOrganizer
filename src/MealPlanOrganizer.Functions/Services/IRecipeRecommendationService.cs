namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Result DTO for recommended recipes with scoring information.
/// </summary>
public class RecommendedRecipe
{
    public Guid RecipeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    
    /// <summary>
    /// Total recommendation score (0-100).
    /// </summary>
    public double Score { get; set; }
    
    /// <summary>
    /// Average rating across all family members (1-5).
    /// </summary>
    public double AverageRating { get; set; }
    
    /// <summary>
    /// Number of ratings for this recipe.
    /// </summary>
    public int RatingCount { get; set; }
    
    /// <summary>
    /// Last date this recipe was cooked (from meal plans), or null if never.
    /// </summary>
    public DateTime? LastCookedDate { get; set; }
    
    /// <summary>
    /// Most common frequency preference from family members.
    /// </summary>
    public string? FrequencyPreference { get; set; }
    
    /// <summary>
    /// List of reasons this recipe is recommended.
    /// </summary>
    public List<string> ReasonCodes { get; set; } = new();
}

/// <summary>
/// Service for generating smart recipe recommendations for meal planning.
/// </summary>
public interface IRecipeRecommendationService
{
    /// <summary>
    /// Get recommended recipes for a week, sorted by score.
    /// </summary>
    /// <param name="weekStartDate">Start date of the week to plan for.</param>
    /// <returns>List of recipes sorted by recommendation score (highest first).</returns>
    Task<List<RecommendedRecipe>> GetRecommendedRecipesAsync(DateTime weekStartDate);
}
