namespace MealPlanOrganizer.Functions.Models;

/// <summary>
/// Response model for a pending rating.
/// </summary>
public class PendingRatingResponse
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeTitle { get; set; } = string.Empty;
    public string? RecipeImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid MealPlanRecipeId { get; set; }
    public DateTime ServedDate { get; set; }
    public DateTime CreatedUtc { get; set; }
}
