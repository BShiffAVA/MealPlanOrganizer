namespace MealPlanOrganizer.Functions.Data.Entities;

/// <summary>
/// Represents a weekly meal plan for a household.
/// </summary>
public class MealPlan
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Name of the meal plan (e.g., "Week of Feb 10")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// First day of the meal plan week.
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// Last day of the meal plan week.
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// User who created the meal plan (display name or email).
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// When the meal plan was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
    
    /// <summary>
    /// Status of the meal plan: Draft, Active, or Complete.
    /// </summary>
    public string Status { get; set; } = "Draft";
    
    /// <summary>
    /// Navigation property to meal plan recipes.
    /// </summary>
    public ICollection<MealPlanRecipe> Recipes { get; set; } = new List<MealPlanRecipe>();
}
