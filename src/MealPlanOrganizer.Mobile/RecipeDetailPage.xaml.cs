using System.Globalization;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

/// <summary>
/// Value converter that returns true if the string is not null or empty.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return !string.IsNullOrEmpty(value as string);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

public partial class RecipeDetailPage : ContentPage
{
	private readonly IRecipeService _recipeService;
	private readonly Guid _recipeId;
	private int _selectedRating = 0;
	private Button[] _starButtons = Array.Empty<Button>();
	private RecipeDetailDto? _currentRecipe;

	public RecipeDetailPage(Guid recipeId)
	{
		InitializeComponent();
		_recipeId = recipeId;
		
		// Get service from dependency injection
		_recipeService = IPlatformApplication.Current?.Services.GetService<IRecipeService>()
			?? throw new InvalidOperationException("IRecipeService not registered");

		// Initialize star buttons array
		_starButtons = new[] { Star1Button, Star2Button, Star3Button, Star4Button, Star5Button };

		// Wire up comments character count
		CommentsEditor.TextChanged += OnCommentsTextChanged;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadRecipeAsync();
	}

	private async void OnEditClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new EditRecipePage(_recipeId));
	}

	private void OnCommentsTextChanged(object? sender, TextChangedEventArgs e)
	{
		var length = e.NewTextValue?.Length ?? 0;
		CommentsCharCount.Text = $"{length}/500";
	}

	private void OnStarClicked(object? sender, EventArgs e)
	{
		if (sender is not Button clickedButton) return;

		// Determine which star was clicked
		_selectedRating = Array.IndexOf(_starButtons, clickedButton) + 1;

		// Update button colors to show selection
		UpdateStarButtonColors();

		// Update label
		SelectedRatingLabel.Text = $"Selected: {_selectedRating} star{(_selectedRating > 1 ? "s" : "")}";
		SelectedRatingLabel.TextColor = Colors.White;

		// Enable submit button
		SubmitRatingButton.IsEnabled = true;
	}

	private void UpdateStarButtonColors()
	{
		for (int i = 0; i < _starButtons.Length; i++)
		{
			if (i < _selectedRating)
			{
				// Selected stars - use primary color
				_starButtons[i].BackgroundColor = Color.FromArgb("#512BD4"); // Primary color
				_starButtons[i].TextColor = Colors.White;
			}
			else
			{
				// Unselected stars - gray
				_starButtons[i].BackgroundColor = Color.FromArgb("#6B6B6B"); // Gray600
				_starButtons[i].TextColor = Colors.White;
			}
		}
	}

	private async void OnSubmitRatingClicked(object? sender, EventArgs e)
	{
		if (_selectedRating < 1 || _selectedRating > 5)
		{
			await DisplayAlert("Error", "Please select a rating between 1 and 5 stars", "OK");
			return;
		}

		// Disable button to prevent double-submit
		SubmitRatingButton.IsEnabled = false;
		SubmitRatingButton.Text = "Submitting...";
		RatingStatusLabel.IsVisible = false;

		try
		{
			// Get frequency preference
			string? frequencyPreference = null;
			var selectedFrequency = FrequencyPicker.SelectedItem as string;
			if (!string.IsNullOrEmpty(selectedFrequency))
			{
				frequencyPreference = selectedFrequency switch
				{
					"Once a week" => "OnceAWeek",
					"Once a month" => "OnceAMonth",
					"A few times a year" => "AFewTimesAYear",
					"Yearly" => "Yearly",
					"Never" => "Never",
					_ => null
				};
			}

			var comments = string.IsNullOrWhiteSpace(CommentsEditor.Text) ? null : CommentsEditor.Text.Trim();

			var result = await _recipeService.RateRecipeAsync(_recipeId, _selectedRating, comments, frequencyPreference);

			if (result.Success)
			{
				// Show success
				RatingStatusLabel.Text = "✓ Rating submitted successfully!";
				RatingStatusLabel.TextColor = Colors.LightGreen;
				RatingStatusLabel.IsVisible = true;

				// Reset form
				_selectedRating = 0;
				UpdateStarButtonColors();
				SelectedRatingLabel.Text = "Tap a star to select your rating";
				SelectedRatingLabel.TextColor = Color.FromArgb("#9E9E9E");
				CommentsEditor.Text = string.Empty;
				FrequencyPicker.SelectedIndex = -1;

				// Reload recipe to show updated ratings
				await LoadRecipeAsync();

				await DisplayAlert("Success", "Your rating has been submitted!", "OK");
			}
			else if (result.AlreadyRatedToday)
			{
				RatingStatusLabel.Text = "You've already rated this recipe today";
				RatingStatusLabel.TextColor = Colors.Orange;
				RatingStatusLabel.IsVisible = true;

				await DisplayAlert("Already Rated", result.ErrorMessage ?? "You can add another rating tomorrow.", "OK");
			}
			else
			{
				RatingStatusLabel.Text = result.ErrorMessage ?? "Failed to submit rating";
				RatingStatusLabel.TextColor = Colors.Red;
				RatingStatusLabel.IsVisible = true;

				// Offer retry for network errors
				var retry = await DisplayAlert("Error", 
					$"{result.ErrorMessage}\n\nWould you like to try again?", 
					"Retry", "Cancel");
				
				if (retry)
				{
					OnSubmitRatingClicked(sender, e);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			RatingStatusLabel.Text = "An error occurred";
			RatingStatusLabel.TextColor = Colors.Red;
			RatingStatusLabel.IsVisible = true;

			var retry = await DisplayAlert("Error", 
				$"Failed to submit rating: {ex.Message}\n\nWould you like to try again?", 
				"Retry", "Cancel");
			
			if (retry)
			{
				OnSubmitRatingClicked(sender, e);
				return;
			}
		}
		finally
		{
			SubmitRatingButton.Text = "Submit Rating";
			SubmitRatingButton.IsEnabled = _selectedRating > 0;
		}
	}

	private void UpdateStarBreakdown(RecipeDetailDto recipe)
	{
		var totalRatings = recipe.RatingCount;
		
		AvgRatingLabel.Text = totalRatings > 0 
			? $"⭐ {recipe.AverageRating:0.0} average ({totalRatings} rating{(totalRatings != 1 ? "s" : "")})"
			: "No ratings yet";

		// Calculate breakdown from ratings list if StarBreakdown not populated
		var breakdown = recipe.StarBreakdown;
		if (breakdown == null || breakdown.Count == 0)
		{
			breakdown = new Dictionary<int, int>
			{
				{ 1, recipe.Ratings.Count(r => r.Rating == 1) },
				{ 2, recipe.Ratings.Count(r => r.Rating == 2) },
				{ 3, recipe.Ratings.Count(r => r.Rating == 3) },
				{ 4, recipe.Ratings.Count(r => r.Rating == 4) },
				{ 5, recipe.Ratings.Count(r => r.Rating == 5) }
			};
		}

		// Update progress bars and counts
		UpdateStarRow(Star5Progress, Star5Count, breakdown.GetValueOrDefault(5), totalRatings);
		UpdateStarRow(Star4Progress, Star4Count, breakdown.GetValueOrDefault(4), totalRatings);
		UpdateStarRow(Star3Progress, Star3Count, breakdown.GetValueOrDefault(3), totalRatings);
		UpdateStarRow(Star2Progress, Star2Count, breakdown.GetValueOrDefault(2), totalRatings);
		UpdateStarRow(Star1Progress, Star1Count, breakdown.GetValueOrDefault(1), totalRatings);
	}

	private static void UpdateStarRow(ProgressBar progressBar, Label countLabel, int count, int total)
	{
		progressBar.Progress = total > 0 ? (double)count / total : 0;
		countLabel.Text = count.ToString();
	}

	private void UpdateUserPersonalRating(RecipeDetailDto recipe)
	{
		var userRating = recipe.UserPersonalRating;
		
		if (userRating != null)
		{
			UserRatingFrame.IsVisible = true;
			UserRatingStars.Text = new string('★', userRating.Rating) + new string('☆', 5 - userRating.Rating);
			UserRatingDate.Text = userRating.RatedUtc.ToString("MMM d, yyyy");
			
			if (!string.IsNullOrEmpty(userRating.FrequencyPreference))
			{
				var displayFreq = userRating.FrequencyPreference switch
				{
					"OnceAWeek" => "Once a week",
					"OnceAMonth" => "Once a month",
					"AFewTimesAYear" => "A few times a year",
					"Yearly" => "Yearly",
					"Never" => "Never",
					_ => userRating.FrequencyPreference
				};
				UserRatingFrequency.Text = $"Frequency: {displayFreq}";
				UserRatingFrequency.IsVisible = true;
			}
			else
			{
				UserRatingFrequency.IsVisible = false;
			}
			
			UserRatingComments.Text = userRating.Comments ?? "";
			UserRatingComments.IsVisible = !string.IsNullOrEmpty(userRating.Comments);
		}
		else
		{
			UserRatingFrame.IsVisible = false;
		}
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

			_currentRecipe = recipe;

			// Populate UI
			TitleLabel.Text = recipe.Title;
			CuisineLabel.Text = $"🍴 {recipe.CuisineType ?? "Unknown"}";
			RatingLabel.Text = recipe.RatingCount > 0 
				? $"⭐ {recipe.AverageRating:0.0} ({recipe.RatingCount} ratings)" 
				: "No ratings yet";
			
			DescriptionLabel.Text = recipe.Description ?? "No description available";
			
			if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
			{
				RecipeImage.Source = ImageSource.FromUri(new Uri(recipe.ImageUrl));
				ImageFrame.IsVisible = true;
			}
			else
			{
				ImageFrame.IsVisible = false;
			}
			
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

			// Update ratings UI
			UpdateStarBreakdown(recipe);
			UpdateUserPersonalRating(recipe);

			// Bind collections
			IngredientsCollection.ItemsSource = recipe.Ingredients;
			StepsCollection.ItemsSource = recipe.Steps;
			RatingsCollection.ItemsSource = recipe.Ratings;

			// Reset rating form
			_selectedRating = 0;
			UpdateStarButtonColors();
			SubmitRatingButton.IsEnabled = false;
			RatingStatusLabel.IsVisible = false;

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
