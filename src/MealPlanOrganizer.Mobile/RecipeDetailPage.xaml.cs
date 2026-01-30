using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class RecipeDetailPage : ContentPage
{
	private readonly IRecipeService _recipeService;
	private readonly Guid _recipeId;

	public RecipeDetailPage(Guid recipeId)
	{
		InitializeComponent();
		_recipeId = recipeId;
		
		// Get service from dependency injection
		_recipeService = IPlatformApplication.Current?.Services.GetService<IRecipeService>()
			?? throw new InvalidOperationException("IRecipeService not registered");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadRecipeAsync();
	}

	private async Task LoadRecipeAsync()
	{
		try
		{
			LoadingIndicator.IsVisible = true;
			LoadingIndicator.IsRunning = true;
			ContentContainer.IsVisible = false;

			var recipe = await _recipeService.GetRecipeByIdAsync(_recipeId);

			if (recipe == null)
			{
				await DisplayAlert("Error", "Recipe not found", "OK");
				await Navigation.PopAsync();
				return;
			}

			// Populate UI
			TitleLabel.Text = recipe.Title;
			CuisineLabel.Text = $"🍴 {recipe.CuisineType ?? "Unknown"}";
			RatingLabel.Text = recipe.RatingCount > 0 
				? $"⭐ {recipe.AverageRating:0.0} ({recipe.RatingCount} ratings)" 
				: "No ratings yet";
			
			DescriptionLabel.Text = recipe.Description ?? "No description available";
			
			PrepTimeLabel.Text = recipe.PrepTimeMinutes.HasValue 
				? $"{recipe.PrepTimeMinutes} min" 
				: "N/A";
			CookTimeLabel.Text = recipe.CookTimeMinutes.HasValue 
				? $"{recipe.CookTimeMinutes} min" 
				: "N/A";
			ServingsLabel.Text = recipe.Servings.HasValue 
				? $"{recipe.Servings}" 
				: "N/A";
			
			CreatorLabel.Text = $"{recipe.CreatedBy ?? "Unknown"} • {recipe.CreatedUtc:MMM d, yyyy}";

			// Bind collections
			IngredientsCollection.ItemsSource = recipe.Ingredients;
			StepsCollection.ItemsSource = recipe.Steps;

			LoadingIndicator.IsVisible = false;
			LoadingIndicator.IsRunning = false;
			ContentContainer.IsVisible = true;
		}
		catch (Exception ex)
		{
			LoadingIndicator.IsVisible = false;
			LoadingIndicator.IsRunning = false;
			await DisplayAlert("Error", $"Failed to load recipe: {ex.Message}", "OK");
			await Navigation.PopAsync();
		}
	}
}
