namespace MealPlanOrganizer.Functions.Data.Entities;

public class RecipeRating
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5 stars
    public string? Comments { get; set; }
    public string? FrequencyPreference { get; set; } // OnceAWeek, OnceAMonth, AFewTimesAYear, Yearly, Never
    public DateTime RatedUtc { get; set; }

    // Navigation property
    public Recipe? Recipe { get; set; }
}
