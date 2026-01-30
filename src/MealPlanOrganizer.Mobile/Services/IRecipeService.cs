namespace MealPlanOrganizer.Mobile.Services;

public interface IRecipeService
{
    Task<List<RecipeDto>> GetRecipesAsync();
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
