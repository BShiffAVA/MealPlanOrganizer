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
    
    // =============================================
    // Meal Plan Methods
    // =============================================
    
    /// <summary>
    /// Get recommended recipes for meal planning, sorted by smart recommendation score.
    /// </summary>
    Task<RecommendedRecipesResponse?> GetRecommendedRecipesAsync(DateTime? weekStartDate = null);
    
    /// <summary>
    /// Get all meal plans for the household.
    /// </summary>
    Task<MealPlansListResponse?> GetMealPlansAsync();
    
    /// <summary>
    /// Get a specific meal plan with all its days and recipes.
    /// </summary>
    Task<MealPlanDetailDto?> GetMealPlanAsync(Guid mealPlanId);
    
    /// <summary>
    /// Create a new meal plan.
    /// </summary>
    Task<CreateMealPlanResponse> CreateMealPlanAsync(CreateMealPlanDto request);
    
    /// <summary>
    /// Add a recipe to a specific day in a meal plan.
    /// </summary>
    Task<AddRecipeToMealPlanResponse> AddRecipeToMealPlanAsync(Guid mealPlanId, AddRecipeToMealPlanDto request);
    
    /// <summary>
    /// Remove a recipe from a specific day in a meal plan.
    /// </summary>
    Task<RemoveRecipeFromMealPlanResponse> RemoveRecipeFromMealPlanAsync(Guid mealPlanId, DateTime day);
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

// =============================================
// Meal Plan DTOs
// =============================================

/// <summary>
/// A recommended recipe with scoring information for meal planning.
/// </summary>
public class RecommendedRecipeDto
{
    public Guid RecipeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public double Score { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public string? LastCookedDate { get; set; }
    public string? FrequencyPreference { get; set; }
    public List<string> ReasonCodes { get; set; } = new();
}

/// <summary>
/// Response from the recommendations API.
/// </summary>
public class RecommendedRecipesResponse
{
    public string WeekStartDate { get; set; } = string.Empty;
    public int TotalRecipes { get; set; }
    public List<RecommendedRecipeDto> Recipes { get; set; } = new();
}

/// <summary>
/// Summary of a meal plan for list views.
/// </summary>
public class MealPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public int TotalDays { get; set; }
    public int RecipesAssigned { get; set; }
}

/// <summary>
/// Response from listing meal plans.
/// </summary>
public class MealPlansListResponse
{
    public int TotalMealPlans { get; set; }
    public List<MealPlanDto> MealPlans { get; set; } = new();
}

/// <summary>
/// A recipe assigned to a day in a meal plan.
/// </summary>
public class MealPlanDayRecipeDto
{
    public Guid AssignmentId { get; set; }
    public Guid RecipeId { get; set; }
    public string? RecipeTitle { get; set; }
    public string? RecipeImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
}

/// <summary>
/// A single day in a meal plan with its assigned recipe.
/// </summary>
public class MealPlanDayDto
{
    public string Date { get; set; } = string.Empty;
    public string DayOfWeek { get; set; } = string.Empty;
    public MealPlanDayRecipeDto? Recipe { get; set; }
}

/// <summary>
/// Full details of a meal plan including all days.
/// </summary>
public class MealPlanDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public int TotalDays { get; set; }
    public int RecipesAssigned { get; set; }
    public List<MealPlanDayDto> Days { get; set; } = new();
}

/// <summary>
/// Request to create a new meal plan.
/// </summary>
public class CreateMealPlanDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Request to add a recipe to a meal plan day.
/// </summary>
public class AddRecipeToMealPlanDto
{
    public Guid RecipeId { get; set; }
    public DateTime Day { get; set; }
}

/// <summary>
/// Response from creating a new meal plan.
/// </summary>
public class CreateMealPlanResponse
{
    public bool Success { get; set; }
    public Guid? MealPlanId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response from adding a recipe to meal plan.
/// </summary>
public class AddRecipeToMealPlanResponse
{
    public bool Success { get; set; }
    public Guid? AssignmentId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response from removing a recipe from meal plan.
/// </summary>
public class RemoveRecipeFromMealPlanResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}