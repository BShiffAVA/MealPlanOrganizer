namespace MealPlanOrganizer.Functions.Data.Entities;

/// <summary>
/// Tracks a pending rating request for a recipe that was served.
/// Created when a meal plan recipe's date passes (at 8pm household time).
/// One entry per user per served recipe.
/// </summary>
public class PendingRating
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The household this pending rating belongs to.
    /// </summary>
    public Guid HouseholdId { get; set; }
    
    /// <summary>
    /// The user who should rate this recipe.
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// The recipe to be rated.
    /// </summary>
    public Guid RecipeId { get; set; }
    
    /// <summary>
    /// The meal plan the recipe was part of.
    /// </summary>
    public Guid MealPlanId { get; set; }
    
    /// <summary>
    /// The specific meal plan recipe entry (for tracking which day it was served).
    /// </summary>
    public Guid MealPlanRecipeId { get; set; }
    
    /// <summary>
    /// The date the recipe was served (dinner date).
    /// </summary>
    public DateTime ServedDate { get; set; }
    
    /// <summary>
    /// When this pending rating record was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
    
    /// <summary>
    /// Status: Pending, Completed, or Dismissed.
    /// </summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>
    /// When the rating was completed or dismissed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
    
    // Navigation properties
    public Household? Household { get; set; }
    public User? User { get; set; }
    public Recipe? Recipe { get; set; }
    public MealPlan? MealPlan { get; set; }
    public MealPlanRecipe? MealPlanRecipeEntry { get; set; }
}
