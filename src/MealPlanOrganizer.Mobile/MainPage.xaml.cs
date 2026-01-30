using System.Collections.ObjectModel;
using System.Linq;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class MainPage : ContentPage
{
	private readonly IRecipeService _recipeService;
	private readonly List<RecipeCard> _allRecipes = new();
	private static readonly List<string> PrepTimeOptions = new()
	{
		"All",
		"Quick (<15)",
		"15-30 min",
		"30-60 min",
		"60+ min"
	};
	private static readonly List<string> RatingOptions = new()
	{
		"All",
		"4-5 stars",
		"3+ stars"
	};
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
			
			_allRecipes.Clear();
			foreach (var recipe in recipes)
			{
				_allRecipes.Add(new RecipeCard(
					recipe.Id,
					recipe.Title,
					recipe.CuisineType ?? "Unknown",
					recipe.PrepTimeMinutes ?? 0,
					recipe.AverageRating,
					recipe.CreatedBy ?? "Unknown"
				));
			}

			InitializeFilters();
			ApplyFilters();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to load recipes: {ex.Message}", "OK");
		}
	}

	private void InitializeFilters()
	{
		var cuisines = _allRecipes
			.Select(r => r.CuisineType)
			.Distinct()
			.OrderBy(c => c)
			.ToList();
		cuisines.Insert(0, "All");
		
		var creators = _allRecipes
			.Select(r => r.CreatedBy)
			.Distinct()
			.OrderBy(c => c)
			.ToList();
		creators.Insert(0, "All");

		CuisinePicker.ItemsSource = cuisines;
		PrepTimePicker.ItemsSource = PrepTimeOptions;
		RatingPicker.ItemsSource = RatingOptions;
		CreatorPicker.ItemsSource = creators;

		CuisinePicker.SelectedIndex = 0;
		PrepTimePicker.SelectedIndex = 0;
		RatingPicker.SelectedIndex = 0;
		CreatorPicker.SelectedIndex = 0;
	}

	private void ApplyFilters()
	{
		var searchText = SearchBar.Text?.Trim() ?? string.Empty;
		var cuisineFilter = CuisinePicker.SelectedItem as string ?? "All";
		var prepTimeFilter = PrepTimePicker.SelectedItem as string ?? "All";
		var ratingFilter = RatingPicker.SelectedItem as string ?? "All";
		var creatorFilter = CreatorPicker.SelectedItem as string ?? "All";

		IEnumerable<RecipeCard> filtered = _allRecipes;

		if (!string.IsNullOrWhiteSpace(searchText))
		{
			filtered = filtered.Where(r => r.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));
		}

		if (cuisineFilter != "All")
		{
			filtered = filtered.Where(r => r.CuisineType.Equals(cuisineFilter, StringComparison.OrdinalIgnoreCase));
		}

		filtered = prepTimeFilter switch
		{
			"Quick (<15)" => filtered.Where(r => r.PrepTimeMinutes < 15),
			"15-30 min" => filtered.Where(r => r.PrepTimeMinutes >= 15 && r.PrepTimeMinutes <= 30),
			"30-60 min" => filtered.Where(r => r.PrepTimeMinutes > 30 && r.PrepTimeMinutes <= 60),
			"60+ min" => filtered.Where(r => r.PrepTimeMinutes > 60),
			_ => filtered
		};

		filtered = ratingFilter switch
		{
			"4-5 stars" => filtered.Where(r => r.Rating >= 4),
			"3+ stars" => filtered.Where(r => r.Rating >= 3),
			_ => filtered
		};

		if (creatorFilter != "All")
		{
			filtered = filtered.Where(r => r.CreatedBy.Equals(creatorFilter, StringComparison.OrdinalIgnoreCase));
		}

		Recipes.Clear();
		foreach (var recipe in filtered)
		{
			Recipes.Add(recipe);
		}
	}

	private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();
	private void OnCuisineFilterChanged(object? sender, EventArgs e) => ApplyFilters();
	private void OnPrepTimeFilterChanged(object? sender, EventArgs e) => ApplyFilters();
	private void OnRatingFilterChanged(object? sender, EventArgs e) => ApplyFilters();
	private void OnCreatorFilterChanged(object? sender, EventArgs e) => ApplyFilters();

	private async void OnRecipeTapped(object? sender, EventArgs e)
	{
		if (sender is Frame frame && frame.BindingContext is RecipeCard recipe)
		{
			await Navigation.PushAsync(new RecipeDetailPage(recipe.Id));
		}
	}

	private async void OnAddRecipeClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new AddRecipePage());
	}
}

public sealed class RecipeCard
{
	public RecipeCard(Guid id, string title, string cuisineType, int prepTimeMinutes, double rating, string createdBy)
	{
		Id = id;
		Title = title;
		CuisineType = cuisineType;
		PrepTimeMinutes = prepTimeMinutes;
		Rating = rating;
		CreatedBy = createdBy;
	}

	public Guid Id { get; }
	public string Title { get; }
	public string CuisineType { get; }
	public int PrepTimeMinutes { get; }
	public double Rating { get; }
	public string CreatedBy { get; }

	public string PrepTimeDisplay => $"Prep {PrepTimeMinutes} min";
	public string RatingDisplay => $"★ {Rating:0.0}";
}
