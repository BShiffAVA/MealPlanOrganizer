namespace MealPlanOrganizer.Mobile.Services;

public class UpdateRecipeDto
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
