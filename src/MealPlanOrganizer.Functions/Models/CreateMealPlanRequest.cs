namespace MealPlanOrganizer.Functions.Models;

/// <summary>
/// Request model for creating a new meal plan.
/// </summary>
public class CreateMealPlanRequest
{
    /// <summary>
    /// Name of the meal plan (e.g., "Week of Feb 10")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Start date of the meal plan (typically a Monday).
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// End date of the meal plan (typically StartDate + 6 days).
    /// </summary>
    public DateTime EndDate { get; set; }
}
