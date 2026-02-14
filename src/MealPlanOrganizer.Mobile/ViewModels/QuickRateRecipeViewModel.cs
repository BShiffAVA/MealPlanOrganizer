using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for QuickRateRecipePage. Handles rating recipes from push notification reminders.
/// Shows pending ratings one at a time for quick rating submission.
/// </summary>
public partial class QuickRateRecipeViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<QuickRateRecipeViewModel> _logger;

    #region Observable Properties

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// All pending ratings to process.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PendingRatingDto> _pendingRatings = new();

    /// <summary>
    /// The currently displayed pending rating.
    /// </summary>
    [ObservableProperty]
    private PendingRatingDto? _currentRating;

    /// <summary>
    /// Index of the current rating in the list.
    /// </summary>
    [ObservableProperty]
    private int _currentIndex;

    /// <summary>
    /// Display text for progress (e.g., "1 of 3").
    /// </summary>
    [ObservableProperty]
    private string _progressText = string.Empty;

    /// <summary>
    /// Whether there are any pending ratings.
    /// </summary>
    [ObservableProperty]
    private bool _hasPendingRatings;

    /// <summary>
    /// Whether all ratings have been processed.
    /// </summary>
    [ObservableProperty]
    private bool _isComplete;

    /// <summary>
    /// Number of ratings completed in this session.
    /// </summary>
    [ObservableProperty]
    private int _ratingsCompleted;

    // Rating form properties
    [ObservableProperty]
    private int _selectedRating;

    [ObservableProperty]
    private string _selectedRatingText = "Tap a star to rate";

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

    // Star button colors
    [ObservableProperty]
    private Color _star1Color = Color.FromArgb("#4A4A4A");

    [ObservableProperty]
    private Color _star2Color = Color.FromArgb("#4A4A4A");

    [ObservableProperty]
    private Color _star3Color = Color.FromArgb("#4A4A4A");

    [ObservableProperty]
    private Color _star4Color = Color.FromArgb("#4A4A4A");

    [ObservableProperty]
    private Color _star5Color = Color.FromArgb("#4A4A4A");

    // Status message
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private Color _statusMessageColor = Colors.White;

    [ObservableProperty]
    private bool _showStatus;

    #endregion

    public List<string> FrequencyOptions { get; } = new()
    {
        "Once a week",
        "Once a month",
        "A few times a year",
        "Yearly",
        "Never"
    };

    public QuickRateRecipeViewModel(
        IRecipeService recipeService,
        INavigationService navigationService,
        ILogger<QuickRateRecipeViewModel> logger)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the view model and load pending ratings.
    /// </summary>
    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            _logger.LogInformation("Loading pending ratings");

            var pendingRatings = await _recipeService.GetPendingRatingsAsync();

            if (pendingRatings.Count == 0)
            {
                _logger.LogInformation("No pending ratings found");
                HasPendingRatings = false;
                IsComplete = true;
                return;
            }

            PendingRatings = new ObservableCollection<PendingRatingDto>(pendingRatings);
            HasPendingRatings = true;
            CurrentIndex = 0;
            ShowCurrentRating();

            _logger.LogInformation("Loaded {Count} pending ratings", pendingRatings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending ratings");
            HasError = true;
            ErrorMessage = "Failed to load recipes to rate. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowCurrentRating()
    {
        if (CurrentIndex >= PendingRatings.Count)
        {
            // All ratings processed
            IsComplete = true;
            HasPendingRatings = false;
            return;
        }

        CurrentRating = PendingRatings[CurrentIndex];
        ProgressText = $"{CurrentIndex + 1} of {PendingRatings.Count}";

        // Reset form
        ResetRatingForm();
    }

    private void ResetRatingForm()
    {
        SelectedRating = 0;
        Comments = string.Empty;
        SelectedFrequency = null;
        ShowStatus = false;
        CanSubmitRating = false;
        SubmitButtonText = "Submit Rating";
        UpdateStarColors();
        SelectedRatingText = "Tap a star to rate";
        SelectedRatingTextColor = Color.FromArgb("#9E9E9E");
    }

    partial void OnSelectedRatingChanged(int value)
    {
        UpdateStarColors();
        CanSubmitRating = value >= 1 && value <= 5;

        SelectedRatingText = value switch
        {
            1 => "😟 Poor",
            2 => "😕 Below Average",
            3 => "😐 Average",
            4 => "🙂 Good",
            5 => "😍 Excellent!",
            _ => "Tap a star to rate"
        };

        SelectedRatingTextColor = value > 0 ? Color.FromArgb("#FFD700") : Color.FromArgb("#9E9E9E");
    }

    partial void OnCommentsChanged(string value)
    {
        CommentsLength = value?.Length ?? 0;
    }

    private void UpdateStarColors()
    {
        var selected = Color.FromArgb("#FFD700");
        var unselected = Color.FromArgb("#4A4A4A");

        Star1Color = SelectedRating >= 1 ? selected : unselected;
        Star2Color = SelectedRating >= 2 ? selected : unselected;
        Star3Color = SelectedRating >= 3 ? selected : unselected;
        Star4Color = SelectedRating >= 4 ? selected : unselected;
        Star5Color = SelectedRating >= 5 ? selected : unselected;
    }

    [RelayCommand]
    private void SelectRating(int rating)
    {
        SelectedRating = rating;
    }

    [RelayCommand]
    private async Task SubmitRatingAsync()
    {
        if (CurrentRating == null || SelectedRating < 1 || SelectedRating > 5)
        {
            StatusMessage = "Please select a rating";
            StatusMessageColor = Colors.Orange;
            ShowStatus = true;
            return;
        }

        IsSubmittingRating = true;
        CanSubmitRating = false;
        SubmitButtonText = "Submitting...";
        ShowStatus = false;

        try
        {
            // Convert frequency to API format
            string? frequencyPreference = SelectedFrequency switch
            {
                "Once a week" => "OnceAWeek",
                "Once a month" => "OnceAMonth",
                "A few times a year" => "AFewTimesAYear",
                "Yearly" => "Yearly",
                "Never" => "Never",
                _ => null
            };

            var comments = string.IsNullOrWhiteSpace(Comments) ? null : Comments.Trim();

            // Submit the rating
            var result = await _recipeService.RateRecipeAsync(
                CurrentRating.RecipeId,
                SelectedRating,
                comments,
                frequencyPreference);

            if (result.Success)
            {
                // Mark the pending rating as completed
                await _recipeService.CompletePendingRatingAsync(CurrentRating.Id);

                RatingsCompleted++;
                _logger.LogInformation("Rating submitted successfully for recipe {RecipeId}", CurrentRating.RecipeId);

                // Move to next rating
                CurrentIndex++;
                ShowCurrentRating();
            }
            else if (result.AlreadyRatedToday)
            {
                StatusMessage = "You've already rated this recipe today";
                StatusMessageColor = Colors.Orange;
                ShowStatus = true;

                // Skip to next
                CurrentIndex++;
                ShowCurrentRating();
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Failed to submit rating";
                StatusMessageColor = Colors.Red;
                ShowStatus = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit rating");
            StatusMessage = "An error occurred. Please try again.";
            StatusMessageColor = Colors.Red;
            ShowStatus = true;
        }
        finally
        {
            IsSubmittingRating = false;
            SubmitButtonText = "Submit Rating";
            CanSubmitRating = SelectedRating > 0;
        }
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        if (CurrentRating == null) return;

        try
        {
            _logger.LogInformation("Skipping rating for recipe {RecipeId}", CurrentRating.RecipeId);

            // Dismiss the pending rating
            await _recipeService.DismissPendingRatingAsync(CurrentRating.Id);

            // Move to next rating
            CurrentIndex++;
            ShowCurrentRating();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to skip rating");
        }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        _logger.LogInformation("Closing quick rate page. Completed {Count} ratings.", RatingsCompleted);
        await _navigationService.GoBackAsync();
    }

    [RelayCommand]
    private async Task ViewRecipeAsync()
    {
        if (CurrentRating == null) return;

        _logger.LogInformation("Navigating to recipe detail for {RecipeId}", CurrentRating.RecipeId);
        await _navigationService.GoToAsync($"{nameof(RecipeDetailPage)}?recipeId={CurrentRating.RecipeId}");
    }
}
