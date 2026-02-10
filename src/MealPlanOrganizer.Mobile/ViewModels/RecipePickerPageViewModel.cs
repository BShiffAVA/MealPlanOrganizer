using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the Recipe Picker page used when selecting recipes for meal plans.
/// Supports both single-select and multi-select modes.
/// </summary>
[QueryProperty(nameof(MealPlanIdString), "mealPlanId")]
[QueryProperty(nameof(DayString), "day")]
[QueryProperty(nameof(StartDateString), "startDate")]
[QueryProperty(nameof(TotalDaysString), "totalDays")]
[QueryProperty(nameof(Mode), "mode")]
public partial class RecipePickerPageViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly ILogger<RecipePickerPageViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<RecipePickerItemViewModel> _recipes = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmptyState;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isMultiSelectMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionCountText))]
    [NotifyPropertyChangedFor(nameof(CanSelectMore))]
    [NotifyPropertyChangedFor(nameof(HasSelections))]
    private int _selectedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionCountText))]
    [NotifyPropertyChangedFor(nameof(CanSelectMore))]
    [NotifyPropertyChangedFor(nameof(SelectionHintText))]
    private int _maxSelections = 7;

    [ObservableProperty]
    private string? _pageTitle;

    [ObservableProperty]
    private string? _dayLabelText;

    // Query properties as strings (Shell passes strings)
    private string? _mealPlanIdString;
    private string? _dayString;
    private string? _startDateString;
    private string? _totalDaysString;
    private string? _mode;

    private Guid _mealPlanId;
    private DateTime? _dayDate;
    private DateTime? _startDate;
    private int _totalDays;
    private bool _hasLoaded;

    public string? MealPlanIdString
    {
        get => _mealPlanIdString;
        set
        {
            _mealPlanIdString = value;
            if (Guid.TryParse(value, out var guid))
            {
                _mealPlanId = guid;
            }
            OnPropertyChanged();
            TryLoadIfReady();
        }
    }

    public string? DayString
    {
        get => _dayString;
        set
        {
            _dayString = value;
            if (DateTime.TryParse(value, out var dayDate))
            {
                _dayDate = dayDate;
            }
            OnPropertyChanged();
            UpdatePageTitle();
        }
    }

    public string? StartDateString
    {
        get => _startDateString;
        set
        {
            _startDateString = value;
            if (DateTime.TryParse(value, out var startDate))
            {
                _startDate = startDate;
            }
            OnPropertyChanged();
        }
    }

    public string? TotalDaysString
    {
        get => _totalDaysString;
        set
        {
            _totalDaysString = value;
            if (int.TryParse(value, out var totalDays))
            {
                _totalDays = totalDays;
                MaxSelections = totalDays;
            }
            OnPropertyChanged();
            UpdateMultiSelectMode();
        }
    }

    public string? Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            OnPropertyChanged();
            UpdateMultiSelectMode();
        }
    }

    public string SelectionCountText => SelectedCount == 1 
        ? "1 recipe selected" 
        : $"{SelectedCount} recipes selected";

    public string SelectionHintText => SelectedCount < MaxSelections
        ? $"Select up to {MaxSelections - SelectedCount} more"
        : "Maximum selected";

    public bool CanSelectMore => SelectedCount < MaxSelections;
    public bool HasSelections => SelectedCount > 0;

    public RecipePickerPageViewModel(
        IRecipeService recipeService,
        ILogger<RecipePickerPageViewModel> logger)
    {
        _recipeService = recipeService;
        _logger = logger;
    }

    private void UpdatePageTitle()
    {
        if (IsMultiSelectMode)
        {
            PageTitle = "Select Recipes";
            DayLabelText = $"Tap recipes in order to assign them to your meal plan (up to {MaxSelections})";
        }
        else if (_dayDate.HasValue)
        {
            PageTitle = "Select a Recipe";
            DayLabelText = $"for {_dayDate.Value:dddd, MMMM d}";
        }
        else
        {
            PageTitle = "Select Recipes";
            DayLabelText = "Tap recipes to select them for your meal plan";
        }
    }

    private void UpdateMultiSelectMode()
    {
        IsMultiSelectMode = Mode?.Equals("multi", StringComparison.OrdinalIgnoreCase) == true;
        _logger.LogDebug("Multi-select mode: {Mode}, MaxSelections: {Max}", IsMultiSelectMode, MaxSelections);
        UpdatePageTitle();
    }

    private void TryLoadIfReady()
    {
        if (_hasLoaded) return;
        if (_mealPlanId != Guid.Empty)
        {
            _hasLoaded = true;
            _ = LoadRecommendationsAsync();
        }
    }

    [RelayCommand]
    private async Task LoadRecommendationsAsync()
    {
        try
        {
            IsLoading = true;
            ShowEmptyState = false;
            ErrorMessage = null;

            _logger.LogInformation("Loading recommendations for MealPlanId: {MealPlanId}", _mealPlanId);

            // Get week start date from the day or startDate
            DateTime? weekStart = null;
            if (_startDate.HasValue)
            {
                weekStart = _startDate.Value;
            }
            else if (_dayDate.HasValue)
            {
                // Get the Monday of that week
                var daysFromMonday = ((int)_dayDate.Value.DayOfWeek - 1 + 7) % 7;
                weekStart = _dayDate.Value.AddDays(-daysFromMonday);
            }

            var response = await _recipeService.GetRecommendedRecipesAsync(weekStart);

            Recipes.Clear();

            if (response?.Recipes != null && response.Recipes.Count > 0)
            {
                foreach (var recipe in response.Recipes)
                {
                    Recipes.Add(new RecipePickerItemViewModel(recipe));
                }
                ShowEmptyState = false;
            }
            else
            {
                ShowEmptyState = true;
            }

            _logger.LogInformation("Loaded {Count} recommendations", Recipes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recommendations");
            ErrorMessage = "Failed to load recipes. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectRecipeAsync(RecipePickerItemViewModel? item)
    {
        if (item == null) return;

        try
        {
            if (IsMultiSelectMode)
            {
                HandleMultiSelectTap(item);
            }
            else
            {
                await HandleSingleSelectAsync(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select recipe {RecipeId}", item.RecipeId);
            ErrorMessage = "Failed to select recipe. Please try again.";
        }
    }

    private async Task HandleSingleSelectAsync(RecipePickerItemViewModel item)
    {
        if (!_dayDate.HasValue)
        {
            await Shell.Current.DisplayAlertAsync("Error", "Invalid date", "OK");
            return;
        }

        // Confirm selection
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Add Recipe",
            $"Add \"{item.Title}\" to {_dayDate.Value:dddd, MMMM d}?",
            "Add",
            "Cancel");

        if (!confirm) return;

        try
        {
            IsLoading = true;

            var request = new AddRecipeToMealPlanDto
            {
                RecipeId = item.RecipeId,
                Day = _dayDate.Value
            };

            var result = await _recipeService.AddRecipeToMealPlanAsync(_mealPlanId, request);

            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to add recipe", "OK");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void HandleMultiSelectTap(RecipePickerItemViewModel item)
    {
        if (item.IsSelected)
        {
            // Deselect
            var oldOrder = item.SelectionOrder;
            item.IsSelected = false;
            item.SelectionOrder = 0;
            SelectedCount--;

            // Reorder remaining selections
            foreach (var recipe in Recipes.Where(r => r.IsSelected && r.SelectionOrder > oldOrder))
            {
                recipe.SelectionOrder--;
            }

            _logger.LogDebug("Deselected recipe {RecipeId}, new count: {Count}", item.RecipeId, SelectedCount);
        }
        else if (CanSelectMore)
        {
            // Select
            SelectedCount++;
            item.SelectionOrder = SelectedCount;
            item.IsSelected = true;

            _logger.LogDebug("Selected recipe {RecipeId} as #{Order}", item.RecipeId, item.SelectionOrder);
        }
        else
        {
            // Max reached
            _ = Shell.Current.DisplayAlertAsync("Maximum Reached", 
                $"You can only select up to {MaxSelections} recipes. Deselect one first.", "OK");
        }
    }

    [RelayCommand]
    private async Task DoneAsync()
    {
        if (!IsMultiSelectMode || !HasSelections) return;

        if (!_startDate.HasValue)
        {
            await Shell.Current.DisplayAlertAsync("Error", "Invalid start date", "OK");
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var selectedRecipes = Recipes
                .Where(r => r.IsSelected)
                .OrderBy(r => r.SelectionOrder)
                .ToList();

            _logger.LogInformation("Assigning {Count} recipes to meal plan starting at {StartDate}",
                selectedRecipes.Count, _startDate);

            var currentDay = _startDate.Value;

            foreach (var recipe in selectedRecipes)
            {
                var request = new AddRecipeToMealPlanDto
                {
                    RecipeId = recipe.RecipeId,
                    Day = currentDay
                };

                var result = await _recipeService.AddRecipeToMealPlanAsync(_mealPlanId, request);

                if (!result.Success)
                {
                    await Shell.Current.DisplayAlertAsync("Warning", 
                        $"Failed to add {recipe.Title}: {result.ErrorMessage}", "OK");
                }

                currentDay = currentDay.AddDays(1);
            }

            _logger.LogInformation("Successfully assigned all recipes");
            await Shell.Current.GoToAsync($"../{nameof(MealPlanDetailPage)}?mealPlanId={_mealPlanIdString}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign recipes in bulk");
            ErrorMessage = "Failed to save selections. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
