using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealPlanOrganizer.Mobile.Tests.ViewModels;

/// <summary>
/// Unit tests for QuickRateRecipeViewModel behavior.
/// These tests verify the ViewModel state management and business logic
/// by implementing a testable version of the ViewModel behavior.
/// </summary>
public class QuickRateRecipeViewModelTests
{
    private readonly TestableQuickRateRecipeViewModel _viewModel;
    private readonly MockRecipeServiceForViewModel _mockRecipeService;
    private readonly MockNavigationServiceForViewModel _mockNavigationService;

    public QuickRateRecipeViewModelTests()
    {
        _mockRecipeService = new MockRecipeServiceForViewModel();
        _mockNavigationService = new MockNavigationServiceForViewModel();
        _viewModel = new TestableQuickRateRecipeViewModel(
            _mockRecipeService,
            _mockNavigationService,
            NullLogger<TestableQuickRateRecipeViewModel>.Instance);
    }

    #region Initialize Tests

    [Fact]
    public async Task Initialize_WhenNoPendingRatings_SetsIsCompleteTrue()
    {
        // Arrange
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingData>();

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsComplete.Should().BeTrue();
        _viewModel.HasPendingRatings.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_WhenPendingRatingsExist_SetsHasPendingRatingsTrue()
    {
        // Arrange
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingData>
        {
            CreatePendingRating()
        };

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.HasPendingRatings.Should().BeTrue();
        _viewModel.IsComplete.Should().BeFalse();
        _viewModel.CurrentRating.Should().NotBeNull();
    }

    [Fact]
    public async Task Initialize_ShowsLoadingDuringFetch()
    {
        // Arrange
        var wasLoadingDuringFetch = false;
        _mockRecipeService.OnGetPendingRatings = () =>
        {
            wasLoadingDuringFetch = _viewModel.IsLoading;
        };
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingData>();

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        wasLoadingDuringFetch.Should().BeTrue();
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_WhenServiceThrows_SetsHasErrorTrue()
    {
        // Arrange
        _mockRecipeService.ShouldThrowOnGetPendingRatings = true;

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Initialize_SetsProgressTextCorrectly()
    {
        // Arrange
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingData>
        {
            CreatePendingRating("Recipe 1"),
            CreatePendingRating("Recipe 2"),
            CreatePendingRating("Recipe 3")
        };

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.ProgressText.Should().Be("1 of 3");
    }

    [Fact]
    public async Task Initialize_SetsCurrentRatingToFirst()
    {
        // Arrange
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingData>
        {
            CreatePendingRating("First Recipe"),
            CreatePendingRating("Second Recipe")
        };

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.CurrentRating!.RecipeTitle.Should().Be("First Recipe");
        _viewModel.CurrentIndex.Should().Be(0);
    }

    #endregion

    #region Rating Selection Tests

    [Theory]
    [InlineData(1, "😟 Poor")]
    [InlineData(2, "😕 Below Average")]
    [InlineData(3, "😐 Average")]
    [InlineData(4, "🙂 Good")]
    [InlineData(5, "😍 Excellent!")]
    public void SelectRating_SetsCorrectRatingText(int rating, string expectedText)
    {
        // Act
        _viewModel.SelectRating(rating);

        // Assert
        _viewModel.SelectedRatingText.Should().Be(expectedText);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SelectRating_EnablesSubmitButton(int rating)
    {
        // Act
        _viewModel.SelectRating(rating);

        // Assert
        _viewModel.CanSubmitRating.Should().BeTrue();
    }

    [Fact]
    public void SelectRating_WhenZero_DisablesSubmitButton()
    {
        // Arrange
        _viewModel.SelectRating(3); // First select valid
        
        // Act
        _viewModel.SelectRating(0);

        // Assert
        _viewModel.CanSubmitRating.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, true, false, false, false, false)]
    [InlineData(2, true, true, false, false, false)]
    [InlineData(3, true, true, true, false, false)]
    [InlineData(4, true, true, true, true, false)]
    [InlineData(5, true, true, true, true, true)]
    public void SelectRating_UpdatesStarColorsCorrectly(int rating, bool s1, bool s2, bool s3, bool s4, bool s5)
    {
        // Act
        _viewModel.SelectRating(rating);

        // Assert
        _viewModel.Star1Selected.Should().Be(s1);
        _viewModel.Star2Selected.Should().Be(s2);
        _viewModel.Star3Selected.Should().Be(s3);
        _viewModel.Star4Selected.Should().Be(s4);
        _viewModel.Star5Selected.Should().Be(s5);
    }

    #endregion

    #region Submit Rating Tests

    [Fact]
    public async Task SubmitRating_CallsRateRecipeWithCorrectParameters()
    {
        // Arrange
        await SetupWithPendingRating();
        _viewModel.SelectRating(4);
        _viewModel.Comments = "Great recipe!";
        _viewModel.SelectedFrequency = "Once a week";

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _mockRecipeService.RateRecipeCalls.Should().HaveCount(1);
        var call = _mockRecipeService.RateRecipeCalls[0];
        call.Rating.Should().Be(4);
        call.Comments.Should().Be("Great recipe!");
        call.FrequencyPreference.Should().Be("OnceAWeek");
    }

    [Fact]
    public async Task SubmitRating_WhenSuccessful_MarksCompleteAndAdvances()
    {
        // Arrange
        await SetupWithPendingRatings(2);
        _viewModel.SelectRating(5);
        var firstRatingId = _viewModel.CurrentRating!.Id;

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _mockRecipeService.CompletedPendingRatingIds.Should().Contain(firstRatingId);
        _viewModel.CurrentIndex.Should().Be(1);
        _viewModel.RatingsCompleted.Should().Be(1);
    }

    [Fact]
    public async Task SubmitRating_WhenLastRating_SetsIsCompleteTrue()
    {
        // Arrange
        await SetupWithPendingRating();
        _viewModel.SelectRating(3);

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _viewModel.IsComplete.Should().BeTrue();
        _viewModel.HasPendingRatings.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitRating_WhenNoRatingSelected_ShowsError()
    {
        // Arrange
        await SetupWithPendingRating();
        // Don't select a rating

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _viewModel.ShowStatus.Should().BeTrue();
        _viewModel.StatusMessage.Should().Contain("rating");
        _mockRecipeService.RateRecipeCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitRating_WhenAlreadyRatedToday_SkipsToNext()
    {
        // Arrange
        await SetupWithPendingRatings(2);
        _viewModel.SelectRating(4);
        _mockRecipeService.RateRecipeResultToReturn = new RateRecipeResultData 
        { 
            Success = false, 
            AlreadyRatedToday = true 
        };

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _viewModel.CurrentIndex.Should().Be(1);
    }

    [Fact]
    public async Task SubmitRating_WhenServiceFails_ShowsError()
    {
        // Arrange
        await SetupWithPendingRating();
        _viewModel.SelectRating(4);
        _mockRecipeService.ShouldThrowOnRateRecipe = true;

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _viewModel.ShowStatus.Should().BeTrue();
        _viewModel.StatusMessage.Should().Contain("error");
    }

    [Theory]
    [InlineData("Once a week", "OnceAWeek")]
    [InlineData("Once a month", "OnceAMonth")]
    [InlineData("A few times a year", "AFewTimesAYear")]
    [InlineData("Yearly", "Yearly")]
    [InlineData("Never", "Never")]
    public async Task SubmitRating_ConvertsFrequencyCorrectly(string displayFrequency, string expectedApiFrequency)
    {
        // Arrange
        await SetupWithPendingRating();
        _viewModel.SelectRating(3);
        _viewModel.SelectedFrequency = displayFrequency;

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _mockRecipeService.RateRecipeCalls[0].FrequencyPreference.Should().Be(expectedApiFrequency);
    }

    #endregion

    #region Skip Tests

    [Fact]
    public async Task Skip_DismissesPendingRating()
    {
        // Arrange
        await SetupWithPendingRating();
        var pendingRatingId = _viewModel.CurrentRating!.Id;

        // Act
        await _viewModel.SkipAsync();

        // Assert
        _mockRecipeService.DismissedPendingRatingIds.Should().Contain(pendingRatingId);
    }

    [Fact]
    public async Task Skip_AdvancesToNextRating()
    {
        // Arrange
        await SetupWithPendingRatings(2);

        // Act
        await _viewModel.SkipAsync();

        // Assert
        _viewModel.CurrentIndex.Should().Be(1);
        _viewModel.ProgressText.Should().Be("2 of 2");
    }

    [Fact]
    public async Task Skip_WhenLastRating_SetsIsCompleteTrue()
    {
        // Arrange
        await SetupWithPendingRating();

        // Act
        await _viewModel.SkipAsync();

        // Assert
        _viewModel.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Skip_ResetsFormForNextRating()
    {
        // Arrange
        await SetupWithPendingRatings(2);
        _viewModel.SelectRating(4);
        _viewModel.Comments = "Some comment";
        _viewModel.SelectedFrequency = "Once a week";

        // Act
        await _viewModel.SkipAsync();

        // Assert
        _viewModel.SelectedRating.Should().Be(0);
        _viewModel.Comments.Should().BeEmpty();
        _viewModel.SelectedFrequency.Should().BeNull();
    }

    #endregion

    #region Close Tests

    [Fact]
    public async Task Close_NavigatesBack()
    {
        // Act
        await _viewModel.CloseAsync();

        // Assert
        _mockNavigationService.GoBackCallCount.Should().Be(1);
    }

    #endregion

    #region ViewRecipe Tests

    [Fact]
    public async Task ViewRecipe_NavigatesToRecipeDetail()
    {
        // Arrange
        await SetupWithPendingRating();
        var recipeId = _viewModel.CurrentRating!.RecipeId;

        // Act
        await _viewModel.ViewRecipeAsync();

        // Assert
        _mockNavigationService.NavigationCalls.Should().HaveCount(1);
        _mockNavigationService.NavigationCalls[0].Should().Contain(recipeId.ToString());
    }

    [Fact]
    public async Task ViewRecipe_WhenNoCurrentRating_DoesNothing()
    {
        // Arrange - don't initialize, so CurrentRating is null

        // Act
        await _viewModel.ViewRecipeAsync();

        // Assert
        _mockNavigationService.NavigationCalls.Should().BeEmpty();
    }

    #endregion

    #region Form Reset Tests

    [Fact]
    public async Task AfterSubmit_FormIsResetForNextRating()
    {
        // Arrange
        await SetupWithPendingRatings(2);
        _viewModel.SelectRating(5);
        _viewModel.Comments = "Amazing!";
        _viewModel.SelectedFrequency = "Once a week";

        // Act
        await _viewModel.SubmitRatingAsync();

        // Assert
        _viewModel.SelectedRating.Should().Be(0);
        _viewModel.Comments.Should().BeEmpty();
        _viewModel.SelectedFrequency.Should().BeNull();
        _viewModel.CanSubmitRating.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private PendingRatingData CreatePendingRating(string title = "Test Recipe")
    {
        return new PendingRatingData
        {
            Id = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeTitle = title,
            ServedDate = DateTime.UtcNow.AddDays(-1),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private async Task SetupWithPendingRating()
    {
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingData>
        {
            CreatePendingRating()
        };
        await _viewModel.InitializeAsync();
    }

    private async Task SetupWithPendingRatings(int count)
    {
        _mockRecipeService.PendingRatingsToReturn = Enumerable
            .Range(1, count)
            .Select(i => CreatePendingRating($"Recipe {i}"))
            .ToList();
        await _viewModel.InitializeAsync();
    }

    #endregion
}

#region Test Support Classes

/// <summary>
/// Testable implementation of QuickRateRecipeViewModel behavior.
/// Mirrors the actual ViewModel but without MAUI dependencies.
/// </summary>
public partial class TestableQuickRateRecipeViewModel : ObservableObject
{
    private readonly MockRecipeServiceForViewModel _recipeService;
    private readonly MockNavigationServiceForViewModel _navigationService;
    private readonly ILogger<TestableQuickRateRecipeViewModel> _logger;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private ObservableCollection<PendingRatingData> _pendingRatings = new();
    [ObservableProperty] private PendingRatingData? _currentRating;
    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private bool _hasPendingRatings;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private int _ratingsCompleted;
    [ObservableProperty] private int _selectedRating;
    [ObservableProperty] private string _selectedRatingText = "Tap a star to rate";
    [ObservableProperty] private string _comments = string.Empty;
    [ObservableProperty] private string? _selectedFrequency;
    [ObservableProperty] private bool _canSubmitRating;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showStatus;

    public bool Star1Selected { get; private set; }
    public bool Star2Selected { get; private set; }
    public bool Star3Selected { get; private set; }
    public bool Star4Selected { get; private set; }
    public bool Star5Selected { get; private set; }

    public TestableQuickRateRecipeViewModel(
        MockRecipeServiceForViewModel recipeService,
        MockNavigationServiceForViewModel navigationService,
        ILogger<TestableQuickRateRecipeViewModel> logger)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var pendingRatings = await _recipeService.GetPendingRatingsAsync();

            if (pendingRatings.Count == 0)
            {
                HasPendingRatings = false;
                IsComplete = true;
                return;
            }

            PendingRatings = new ObservableCollection<PendingRatingData>(pendingRatings);
            HasPendingRatings = true;
            CurrentIndex = 0;
            ShowCurrentRating();
        }
        catch (Exception)
        {
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
            IsComplete = true;
            HasPendingRatings = false;
            return;
        }

        CurrentRating = PendingRatings[CurrentIndex];
        ProgressText = $"{CurrentIndex + 1} of {PendingRatings.Count}";
        ResetForm();
    }

    private void ResetForm()
    {
        SelectedRating = 0;
        Comments = string.Empty;
        SelectedFrequency = null;
        ShowStatus = false;
        CanSubmitRating = false;
        UpdateStarColors();
        SelectedRatingText = "Tap a star to rate";
    }

    public void SelectRating(int rating)
    {
        SelectedRating = rating;
        UpdateStarColors();
        CanSubmitRating = rating >= 1 && rating <= 5;

        SelectedRatingText = rating switch
        {
            1 => "😟 Poor",
            2 => "😕 Below Average",
            3 => "😐 Average",
            4 => "🙂 Good",
            5 => "😍 Excellent!",
            _ => "Tap a star to rate"
        };
    }

    private void UpdateStarColors()
    {
        Star1Selected = SelectedRating >= 1;
        Star2Selected = SelectedRating >= 2;
        Star3Selected = SelectedRating >= 3;
        Star4Selected = SelectedRating >= 4;
        Star5Selected = SelectedRating >= 5;
    }

    public async Task SubmitRatingAsync()
    {
        if (CurrentRating == null || SelectedRating < 1 || SelectedRating > 5)
        {
            StatusMessage = "Please select a rating";
            ShowStatus = true;
            return;
        }

        ShowStatus = false;

        try
        {
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

            var result = await _recipeService.RateRecipeAsync(
                CurrentRating.RecipeId,
                SelectedRating,
                comments,
                frequencyPreference);

            if (result.Success)
            {
                await _recipeService.CompletePendingRatingAsync(CurrentRating.Id);
                RatingsCompleted++;
                CurrentIndex++;
                ShowCurrentRating();
            }
            else if (result.AlreadyRatedToday)
            {
                StatusMessage = "You've already rated this recipe today";
                ShowStatus = true;
                CurrentIndex++;
                ShowCurrentRating();
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Failed to submit rating";
                ShowStatus = true;
            }
        }
        catch (Exception)
        {
            StatusMessage = "An error occurred. Please try again.";
            ShowStatus = true;
        }
    }

    public async Task SkipAsync()
    {
        if (CurrentRating == null) return;

        try
        {
            await _recipeService.DismissPendingRatingAsync(CurrentRating.Id);
            CurrentIndex++;
            ShowCurrentRating();
        }
        catch (Exception)
        {
            // Error handled silently in actual impl
        }
    }

    public async Task CloseAsync()
    {
        await _navigationService.GoBackAsync();
    }

    public async Task ViewRecipeAsync()
    {
        if (CurrentRating == null) return;
        await _navigationService.GoToAsync($"RecipeDetailPage?recipeId={CurrentRating.RecipeId}");
    }
}

public class PendingRatingData
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeTitle { get; set; } = string.Empty;
    public string? RecipeImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public DateTime ServedDate { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class RateRecipeResultData
{
    public bool Success { get; set; }
    public bool AlreadyRatedToday { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MockRecipeServiceForViewModel
{
    public List<PendingRatingData> PendingRatingsToReturn { get; set; } = new();
    public bool ShouldThrowOnGetPendingRatings { get; set; }
    public bool ShouldThrowOnRateRecipe { get; set; }
    public RateRecipeResultData RateRecipeResultToReturn { get; set; } = new() { Success = true };
    
    public Action? OnGetPendingRatings { get; set; }
    
    public List<Guid> CompletedPendingRatingIds { get; } = new();
    public List<Guid> DismissedPendingRatingIds { get; } = new();
    public List<(Guid RecipeId, int Rating, string? Comments, string? FrequencyPreference)> RateRecipeCalls { get; } = new();

    public Task<List<PendingRatingData>> GetPendingRatingsAsync()
    {
        OnGetPendingRatings?.Invoke();
        
        if (ShouldThrowOnGetPendingRatings)
            throw new Exception("Mock error");
            
        return Task.FromResult(PendingRatingsToReturn);
    }

    public Task<bool> CompletePendingRatingAsync(Guid id)
    {
        CompletedPendingRatingIds.Add(id);
        return Task.FromResult(true);
    }

    public Task<bool> DismissPendingRatingAsync(Guid id)
    {
        DismissedPendingRatingIds.Add(id);
        return Task.FromResult(true);
    }

    public Task<RateRecipeResultData> RateRecipeAsync(Guid recipeId, int rating, string? comments, string? frequencyPreference)
    {
        if (ShouldThrowOnRateRecipe)
            throw new Exception("Mock rate error");
            
        RateRecipeCalls.Add((recipeId, rating, comments, frequencyPreference));
        return Task.FromResult(RateRecipeResultToReturn);
    }
}

public class MockNavigationServiceForViewModel
{
    public List<string> NavigationCalls { get; } = new();
    public int GoBackCallCount { get; private set; }

    public Task GoToAsync(string route)
    {
        NavigationCalls.Add(route);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        GoBackCallCount++;
        return Task.CompletedTask;
    }
}

#endregion
