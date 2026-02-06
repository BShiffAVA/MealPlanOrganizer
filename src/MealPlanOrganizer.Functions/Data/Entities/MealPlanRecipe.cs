namespace MealPlanOrganizer.Functions.Data.Entities;

/// <summary>
/// Represents a recipe assigned to a specific day in a meal plan.
/// Currently only supports dinner (MealType field deferred).
/// </summary>
public class MealPlanRecipe
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The meal plan this assignment belongs to.
    /// </summary>
    public Guid MealPlanId { get; set; }
    
    /// <summary>
    /// The recipe assigned to this day.
    /// </summary>
    public Guid RecipeId { get; set; }
    
    /// <summary>
    /// The date this recipe is assigned to (dinner).
    /// </summary>
    public DateTime Day { get; set; }
    
    /// <summary>
    /// When this assignment was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
    
    /// <summary>
    /// Navigation property to the meal plan.
    /// </summary>
    public MealPlan? MealPlan { get; set; }
    
    /// <summary>
    /// Navigation property to the recipe.
    /// </summary>
    public Recipe? Recipe { get; set; }
}
