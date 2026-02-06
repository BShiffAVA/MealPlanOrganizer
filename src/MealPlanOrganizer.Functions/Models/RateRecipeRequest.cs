namespace MealPlanOrganizer.Functions.Models;

public class RateRecipeRequest
{
    public int Rating { get; set; } // 1-5
    public string? Comments { get; set; }
    /// <summary>
    /// Optional frequency preference: OnceAWeek, OnceAMonth, AFewTimesAYear, Yearly, Never
    /// </summary>
    public string? FrequencyPreference { get; set; }

    public static readonly string[] ValidFrequencies = 
    {
        "OnceAWeek", "OnceAMonth", "AFewTimesAYear", "Yearly", "Never"
    };
}
