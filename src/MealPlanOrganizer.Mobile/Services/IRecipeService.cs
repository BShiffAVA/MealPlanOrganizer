namespace MealPlanOrganizer.Mobile.Services;

public interface IRecipeService
{
    Task<List<RecipeDto>> GetRecipesAsync();
    Task<RecipeDetailDto?> GetRecipeByIdAsync(Guid id);
}

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public double AverageRating { get; set; }
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
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public DateTime RatedUtc { get; set; }
}
