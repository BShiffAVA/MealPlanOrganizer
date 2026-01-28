namespace MealPlanOrganizer.Functions.Models;

public class RateRecipeRequest
{
    public int Rating { get; set; } // 1-5
    public string? Comments { get; set; }
}
