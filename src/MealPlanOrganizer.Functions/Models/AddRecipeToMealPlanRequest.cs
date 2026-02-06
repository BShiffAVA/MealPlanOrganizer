namespace MealPlanOrganizer.Functions.Models;

/// <summary>
/// Request model for adding a recipe to a meal plan.
/// </summary>
public class AddRecipeToMealPlanRequest
{
    /// <summary>
    /// ID of the recipe to add.
    /// </summary>
    public Guid RecipeId { get; set; }
    
    /// <summary>
    /// The day to assign this recipe to (must be within meal plan date range).
    /// </summary>
    public DateTime Day { get; set; }
}
