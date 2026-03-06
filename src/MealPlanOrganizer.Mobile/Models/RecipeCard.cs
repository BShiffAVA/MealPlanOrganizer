namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// View model for recipe cards displayed in lists.
/// </summary>
public sealed class RecipeCard
{
    public RecipeCard(Guid id, string title, string cuisineType, int prepTimeMinutes, double rating, string createdBy, string imageUrl = "")
    {
        Id = id;
        Title = title;
        CuisineType = cuisineType;
        PrepTimeMinutes = prepTimeMinutes;
        Rating = rating;
        CreatedBy = createdBy;
        ImageUrl = imageUrl;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string CuisineType { get; }
    public int PrepTimeMinutes { get; }
    public double Rating { get; }
    public string CreatedBy { get; }
    public string ImageUrl { get; }

    public string PrepTimeDisplay => $"Prep {PrepTimeMinutes} min";
    public string RatingDisplay => $"★ {Rating:0.0}";
}
