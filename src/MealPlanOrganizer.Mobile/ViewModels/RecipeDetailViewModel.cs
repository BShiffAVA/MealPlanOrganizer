using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for RecipeDetailPage. Handles recipe display, rating submission, and navigation.
/// </summary>
public partial class RecipeDetailViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigationService;
    private readonly IAuthService _authService;
    private readonly ILogger<RecipeDetailViewModel> _logger;

    #region Observable Properties

    [ObservableProperty]
    private RecipeDetailDto? _recipe;

    [ObservableProperty]
    private Guid _recipeId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // Recipe display properties
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _cuisineType = string.Empty;

    [ObservableProperty]
    private string _ratingDisplay = string.Empty;

    [ObservableProperty]
    private string? _imageUrl;

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _isCurrentUserCreator;

    [ObservableProperty]
    private string _prepTime = "N/A";

    [ObservableProperty]
    private string _cookTime = "N/A";

    [ObservableProperty]
    private string _servings = "N/A";

    [ObservableProperty]
    private string _creatorDisplay = string.Empty;

    [ObservableProperty]
    private string _avgRatingDisplay = string.Empty;

    // Star breakdown
    [ObservableProperty]
    private double _star5Progress;

    [ObservableProperty]
    private int _star5Count;

    [ObservableProperty]
    private double _star4Progress;

    [ObservableProperty]
    private int _star4Count;

    [ObservableProperty]
    private double _star3Progress;

    [ObservableProperty]
    private int _star3Count;

    [ObservableProperty]
    private double _star2Progress;

    [ObservableProperty]
    private int _star2Count;

    [ObservableProperty]
    private double _star1Progress;

    [ObservableProperty]
    private int _star1Count;

    // User personal rating
    [ObservableProperty]
    private bool _hasUserRating;

    [ObservableProperty]
    private string _userRatingStars = string.Empty;

    [ObservableProperty]
    private string _userRatingDate = string.Empty;

    [ObservableProperty]
    private string? _userRatingFrequency;

    [ObservableProperty]
    private bool _hasUserRatingFrequency;

    [ObservableProperty]
    private string? _userRatingComments;

    [ObservableProperty]
    private bool _hasUserRatingComments;

    // Rating form properties
    [ObservableProperty]
    private int _selectedRating;

    [ObservableProperty]
    private string _selectedRatingText = "Tap a star to select your rating";

    [ObservableProperty]
    private Color _selectedRatingTextColor = Color.FromArgb("#9E9E9E");

    [ObservableProperty]
    private string _comments = string.Empty;

    [ObservableProperty]
    private int _commentsLength;

    [ObservableProperty]
    private string? _selectedFrequency;

    [ObservableProperty]
    private bool _isSubmittingRating;

    [ObservableProperty]
    private bool _canSubmitRating;

    [ObservableProperty]
    private string _submitButtonText = "Submit Rating";

    [ObservableProperty]
    private string? _ratingStatusMessage;

    [ObservableProperty]
    private Color _ratingStatusColor = Colors.Transparent;

    [ObservableProperty]
    private bool _showRatingStatus;

    // Star button colors
    [ObservableProperty]
    private Color _star1Color = Color.FromArgb("#6B6B6B");

    [ObservableProperty]
    private Color _star2Color = Color.FromArgb("#6B6B6B");

    [ObservableProperty]
    private Color _star3Color = Color.FromArgb("#6B6B6B");

    [ObservableProperty]
    private Color _star4Color = Color.FromArgb("#6B6B6B");

    [ObservableProperty]
    private Color _star5Color = Color.FromArgb("#6B6B6B");

    // Collections
    [ObservableProperty]
    private ObservableCollection<RecipeIngredientDto> _ingredients = new();

    [ObservableProperty]
    private ObservableCollection<RecipeStepDto> _steps = new();

    [ObservableProperty]
    private ObservableCollection<RecipeRatingDto> _ratings = new();

    public List<string> FrequencyOptions { get; } = new()
    {
        "Once a week",
        "Once a month",
        "A few times a year",
        "Yearly",
        "Never"
    };

    #endregion

    public RecipeDetailViewModel(
        IRecipeService recipeService,
        INavigationService navigationService,
        IAuthService authService,
        ILogger<RecipeDetailViewModel> logger)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
        _authService = authService;
        _logger = logger;
    }

    partial void OnCommentsChanged(string value)
    {
        CommentsLength = value?.Length ?? 0;
    }

    partial void OnSelectedRatingChanged(int value)
    {
        UpdateStarColors();
        CanSubmitRating = value > 0;

        if (value > 0)
        {
            SelectedRatingText = $"Selected: {value} star{(value > 1 ? "s" : "")}";
            SelectedRatingTextColor = Colors.White;
        }
        else
        {
            SelectedRatingText = "Tap a star to select your rating";
            SelectedRatingTextColor = Color.FromArgb("#9E9E9E");
        }
    }

    private void UpdateStarColors()
    {
        var selectedColor = Color.FromArgb("#512BD4"); // Primary
        var unselectedColor = Color.FromArgb("#6B6B6B"); // Gray

        Star1Color = SelectedRating >= 1 ? selectedColor : unselectedColor;
        Star2Color = SelectedRating >= 2 ? selectedColor : unselectedColor;
        Star3Color = SelectedRating >= 3 ? selectedColor : unselectedColor;
        Star4Color = SelectedRating >= 4 ? selectedColor : unselectedColor;
        Star5Color = SelectedRating >= 5 ? selectedColor : unselectedColor;
    }

    #region Commands

    [RelayCommand]
    public async Task LoadAsync(Guid recipeId)
    {
        RecipeId = recipeId;
        await LoadRecipeAsync();
    }

    [RelayCommand]
    private async Task LoadRecipeAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var recipe = await _recipeService.GetRecipeByIdAsync(RecipeId);

            if (recipe == null)
            {
                ErrorMessage = "Recipe not found";
                return;
            }

            Recipe = recipe;
            PopulateFromRecipe(recipe);

            // Use the isCurrentUserCreator flag from the API response
            IsCurrentUserCreator = recipe.IsCurrentUserCreator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recipe {RecipeId}", RecipeId);
            ErrorMessage = $"Failed to load recipe: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateFromRecipe(RecipeDetailDto recipe)
    {
        Title = recipe.Title;
        Description = recipe.Description ?? "No description available";
        CuisineType = $"🍴 {recipe.CuisineType ?? "Unknown"}";
        RatingDisplay = recipe.RatingCount > 0
            ? $"⭐ {recipe.AverageRating:0.0} ({recipe.RatingCount} ratings)"
            : "No ratings yet";

        ImageUrl = recipe.ImageUrl;
        HasImage = !string.IsNullOrWhiteSpace(recipe.ImageUrl);

        PrepTime = recipe.PrepTimeMinutes.HasValue ? $"{recipe.PrepTimeMinutes} min" : "N/A";
        CookTime = recipe.CookTimeMinutes.HasValue ? $"{recipe.CookTimeMinutes} min" : "N/A";
        Servings = recipe.Servings.HasValue ? $"{recipe.Servings}" : "N/A";
        CreatorDisplay = $"{recipe.CreatedBy ?? "Unknown"} • {recipe.CreatedUtc:MMM d, yyyy}";

        // Star breakdown
        UpdateStarBreakdown(recipe);

        // User personal rating
        UpdateUserPersonalRating(recipe);

        // Collections
        Ingredients = new ObservableCollection<RecipeIngredientDto>(recipe.Ingredients ?? new List<RecipeIngredientDto>());
        Steps = new ObservableCollection<RecipeStepDto>(recipe.Steps ?? new List<RecipeStepDto>());
        Ratings = new ObservableCollection<RecipeRatingDto>(recipe.Ratings ?? new List<RecipeRatingDto>());

        // Reset rating form
        SelectedRating = 0;
        Comments = string.Empty;
        SelectedFrequency = null;
        ShowRatingStatus = false;
    }

    private void UpdateStarBreakdown(RecipeDetailDto recipe)
    {
        var totalRatings = recipe.RatingCount;

        AvgRatingDisplay = totalRatings > 0
            ? $"⭐ {recipe.AverageRating:0.0} average ({totalRatings} rating{(totalRatings != 1 ? "s" : "")})"
            : "No ratings yet";

        var breakdown = recipe.StarBreakdown;
        var ratings = recipe.Ratings ?? new List<RecipeRatingDto>();
        if (breakdown == null || breakdown.Count == 0)
        {
            breakdown = new Dictionary<int, int>
            {
                { 1, ratings.Count(r => r.Rating == 1) },
                { 2, ratings.Count(r => r.Rating == 2) },
                { 3, ratings.Count(r => r.Rating == 3) },
                { 4, ratings.Count(r => r.Rating == 4) },
                { 5, ratings.Count(r => r.Rating == 5) }
            };
        }

        Star5Count = breakdown.GetValueOrDefault(5);
        Star4Count = breakdown.GetValueOrDefault(4);
        Star3Count = breakdown.GetValueOrDefault(3);
        Star2Count = breakdown.GetValueOrDefault(2);
        Star1Count = breakdown.GetValueOrDefault(1);

        Star5Progress = totalRatings > 0 ? (double)Star5Count / totalRatings : 0;
        Star4Progress = totalRatings > 0 ? (double)Star4Count / totalRatings : 0;
        Star3Progress = totalRatings > 0 ? (double)Star3Count / totalRatings : 0;
        Star2Progress = totalRatings > 0 ? (double)Star2Count / totalRatings : 0;
        Star1Progress = totalRatings > 0 ? (double)Star1Count / totalRatings : 0;
    }

    private void UpdateUserPersonalRating(RecipeDetailDto recipe)
    {
        var userRating = recipe.UserPersonalRating;

        if (userRating != null)
        {
            HasUserRating = true;
            UserRatingStars = new string('★', userRating.Rating) + new string('☆', 5 - userRating.Rating);
            UserRatingDate = userRating.RatedUtc.ToString("MMM d, yyyy");

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
                UserRatingFrequency = $"Frequency: {displayFreq}";
                HasUserRatingFrequency = true;
            }
            else
            {
                HasUserRatingFrequency = false;
            }

            UserRatingComments = userRating.Comments;
            HasUserRatingComments = !string.IsNullOrEmpty(userRating.Comments);
        }
        else
        {
            HasUserRating = false;
        }
    }

    [RelayCommand]
    private void SelectRating(int rating)
    {
        SelectedRating = rating;
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        await _navigationService.GoToAsync($"{nameof(EditRecipePage)}?recipeId={RecipeId}");
    }

    [RelayCommand]
    private async Task SubmitRatingAsync()
    {
        if (SelectedRating < 1 || SelectedRating > 5)
        {
            RatingStatusMessage = "Please select a rating between 1 and 5 stars";
            RatingStatusColor = Colors.Red;
            ShowRatingStatus = true;
            return;
        }

        IsSubmittingRating = true;
        CanSubmitRating = false;
        SubmitButtonText = "Submitting...";
        ShowRatingStatus = false;

        try
        {
            string? frequencyPreference = null;
            if (!string.IsNullOrEmpty(SelectedFrequency))
            {
                frequencyPreference = SelectedFrequency switch
                {
                    "Once a week" => "OnceAWeek",
                    "Once a month" => "OnceAMonth",
                    "A few times a year" => "AFewTimesAYear",
                    "Yearly" => "Yearly",
                    "Never" => "Never",
                    _ => null
                };
            }

            var comments = string.IsNullOrWhiteSpace(Comments) ? null : Comments.Trim();

            var result = await _recipeService.RateRecipeAsync(RecipeId, SelectedRating, comments, frequencyPreference);

            if (result.Success)
            {
                RatingStatusMessage = "✓ Rating submitted successfully!";
                RatingStatusColor = Colors.LightGreen;
                ShowRatingStatus = true;

                // Reset form
                SelectedRating = 0;
                Comments = string.Empty;
                SelectedFrequency = null;

                // Reload recipe to show updated ratings
                await LoadRecipeAsync();
            }
            else if (result.AlreadyRatedToday)
            {
                RatingStatusMessage = "You've already rated this recipe today";
                RatingStatusColor = Colors.Orange;
                ShowRatingStatus = true;
            }
            else
            {
                RatingStatusMessage = result.ErrorMessage ?? "Failed to submit rating";
                RatingStatusColor = Colors.Red;
                ShowRatingStatus = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit rating for recipe {RecipeId}", RecipeId);
            RatingStatusMessage = "An error occurred while submitting rating";
            RatingStatusColor = Colors.Red;
            ShowRatingStatus = true;
        }
        finally
        {
            IsSubmittingRating = false;
            SubmitButtonText = "Submit Rating";
            CanSubmitRating = SelectedRating > 0;
        }
    }

    #endregion
}
