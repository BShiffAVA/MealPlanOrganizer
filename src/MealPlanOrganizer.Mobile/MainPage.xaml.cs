using System.Collections.ObjectModel;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class MainPage : ContentPage
{
	private readonly IRecipeService _recipeService;
	public ObservableCollection<RecipeCard> Recipes { get; } = new();

	public MainPage()
	{
		InitializeComponent();
		BindingContext = this;
		
		// Get service from dependency injection
		_recipeService = IPlatformApplication.Current?.Services.GetService<IRecipeService>()
			?? throw new InvalidOperationException("IRecipeService not registered");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadRecipesAsync();
	}

	private async Task LoadRecipesAsync()
	{
		try
		{
			var recipes = await _recipeService.GetRecipesAsync();
			
			// Clear existing recipes
			Recipes.Clear();
			
			// Add fetched recipes to the collection
			foreach (var recipe in recipes)
			{
				Recipes.Add(new RecipeCard(
					recipe.Id,
					recipe.Title,
					recipe.CuisineType ?? "Unknown",
					recipe.PrepTimeMinutes ?? 0,
					recipe.AverageRating
				));
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to load recipes: {ex.Message}", "OK");
		}
	}

	private async void OnRecipeTapped(object? sender, EventArgs e)
	{
		if (sender is Frame frame && frame.BindingContext is RecipeCard recipe)
		{
			await Navigation.PushAsync(new RecipeDetailPage(recipe.Id));
		}
	}
}

public sealed class RecipeCard
{
	public RecipeCard(Guid id, string title, string cuisineType, int prepTimeMinutes, double rating)
	{
		Id = id;
		Title = title;
		CuisineType = cuisineType;
		PrepTimeMinutes = prepTimeMinutes;
		Rating = rating;
	}

	public Guid Id { get; }
	public string Title { get; }
	public string CuisineType { get; }
	public int PrepTimeMinutes { get; }
	public double Rating { get; }

	public string PrepTimeDisplay => $"Prep {PrepTimeMinutes} min";
	public string RatingDisplay => $"★ {Rating:0.0}";
}
