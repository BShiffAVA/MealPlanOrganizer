namespace MealPlanOrganizer.Functions.Data.Entities;

public class RecipeRating
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5 stars
    public string? Comments { get; set; }
    /// <summary>
    /// When would you next like to eat this meal? Options: RightAway, In2Weeks, NextMonth, NextYear, Never
    /// </summary>
    public string? NextTimePreference { get; set; }
    public DateTime RatedUtc { get; set; }

    // Navigation property
    public Recipe? Recipe { get; set; }
}
