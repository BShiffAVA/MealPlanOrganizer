using MealPlanOrganizer.Mobile.Models;

namespace MealPlanOrganizer.Mobile.Services;

public interface IRecipeService
{
    Task<List<RecipeDto>> GetRecipesAsync();
    Task<RecipeDetailDto?> GetRecipeByIdAsync(Guid id);
    Task<Guid?> CreateRecipeAsync(CreateRecipeDto recipe);
    Task<bool> UpdateRecipeAsync(Guid recipeId, UpdateRecipeDto recipe);
    Task<string?> UploadRecipeImageAsync(FileResult photo, Guid recipeId);
    
    /// <summary>
    /// Extract recipe data from an image, URL, or text using GenAI.
    /// </summary>
    Task<RecipeExtractionResponse?> ExtractRecipeAsync(RecipeExtractionRequest request);
    
    /// <summary>
    /// Submit a rating for a recipe. Returns the result of the rating submission.
    /// </summary>
    Task<RateRecipeResult> RateRecipeAsync(Guid recipeId, int rating, string? comments, string? frequencyPreference);
}

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public double AverageRating { get; set; }
    public string? CreatedBy { get; set; }
}

public class RecipeDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public string? ImageUrl { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    public List<RecipeStepDto> Steps { get; set; } = new();
    public List<RecipeRatingDto> Ratings { get; set; } = new();
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    
    /// <summary>
    /// Star breakdown: count of ratings for each star level (1-5).
    /// </summary>
    public Dictionary<int, int> StarBreakdown { get; set; } = new();
    
    /// <summary>
    /// Current user's most recent personal rating, if available.
    /// </summary>
    public UserPersonalRatingDto? UserPersonalRating { get; set; }
}

public class RecipeIngredientDto
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
}

public class RecipeStepDto
{
    public int StepNumber { get; set; }
    public string Instruction { get; set; } = string.Empty;
}

public class RecipeRatingDto
{
    public Guid RatingId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public string? FrequencyPreference { get; set; }
    public DateTime RatedUtc { get; set; }
    
    /// <summary>
    /// Returns a display-friendly version of the frequency preference.
    /// </summary>
    public string FrequencyDisplay => FrequencyPreference switch
    {
        "OnceAWeek" => "Once a week",
        "OnceAMonth" => "Once a month",
        "AFewTimesAYear" => "A few times a year",
        "Yearly" => "Yearly",
        "Never" => "Never",
        _ => ""
    };
}

/// <summary>
/// Request model for submitting a recipe rating.
/// </summary>
public class RateRecipeRequestDto
{
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public string? FrequencyPreference { get; set; }
}

/// <summary>
/// Result of a rating submission attempt.
/// </summary>
public class RateRecipeResult
{
    public bool Success { get; set; }
    public bool AlreadyRatedToday { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? RatingId { get; set; }
    public double? AverageRating { get; set; }
    public int? TotalRatings { get; set; }
}

/// <summary>
/// Ratings summary for a recipe including star breakdown.
/// </summary>
public class RatingsSummaryDto
{
    public int TotalRatings { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<int, int> StarBreakdown { get; set; } = new();
    public UserPersonalRatingDto? UserPersonalRating { get; set; }
}

/// <summary>
/// The current user's personal rating for a recipe.
/// </summary>
public class UserPersonalRatingDto
{
    public Guid RatingId { get; set; }
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public string? FrequencyPreference { get; set; }
    public DateTime RatedUtc { get; set; }
}

public class CreateRecipeDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public string? ImageUrl { get; set; }
    public List<IngredientInput> Ingredients { get; set; } = new();
    public List<string> Steps { get; set; } = new();
}

public class IngredientInput
{
    public string Name { get; set; } = string.Empty;
    public string? Quantity { get; set; }
}