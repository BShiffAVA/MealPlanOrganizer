using System.Collections.ObjectModel;

namespace MealPlanOrganizer.Mobile;

public partial class MainPage : ContentPage
{
	public ObservableCollection<RecipeCard> Recipes { get; } = new();

	public MainPage()
	{
		InitializeComponent();
		BindingContext = this;
		LoadSampleRecipes();
	}

	private void LoadSampleRecipes()
	{
		Recipes.Add(new RecipeCard("Classic Lasagna", "Italian", 25, 4.6));
		Recipes.Add(new RecipeCard("Chicken Tikka Masala", "Indian", 30, 4.8));
		Recipes.Add(new RecipeCard("Veggie Stir Fry", "Asian", 15, 4.2));
		Recipes.Add(new RecipeCard("Beef Tacos", "Mexican", 20, 4.4));
		Recipes.Add(new RecipeCard("Greek Salad", "Mediterranean", 10, 4.1));
		Recipes.Add(new RecipeCard("Pizza", "Italian", 12, 5.0));
	}
}

public sealed class RecipeCard
{
	public RecipeCard(string title, string cuisineType, int prepTimeMinutes, double rating)
	{
		Title = title;
		CuisineType = cuisineType;
		PrepTimeMinutes = prepTimeMinutes;
		Rating = rating;
	}

	public string Title { get; }
	public string CuisineType { get; }
	public int PrepTimeMinutes { get; }
	public double Rating { get; }

	public string PrepTimeDisplay => $"Prep {PrepTimeMinutes} min";
	public string RatingDisplay => $"★ {Rating:0.0}";
}
