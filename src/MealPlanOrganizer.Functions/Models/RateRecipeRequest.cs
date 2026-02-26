namespace MealPlanOrganizer.Functions.Models;

public class RateRecipeRequest
{
    public int Rating { get; set; } // 1-5
    public string? Comments { get; set; }
    /// <summary>
    /// When would you next like to eat this meal? Options: RightAway, In2Weeks, NextMonth, NextYear, Never
    /// </summary>
    public string? NextTimePreference { get; set; }

    public static readonly string[] ValidNextTimePreferences =
    {
        "RightAway", "In2Weeks", "NextMonth", "NextYear", "Never"
    };
}
