using MealPlanOrganizer.Mobile.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MealPlanOrganizer.Mobile;

[QueryProperty(nameof(MealPlanId), "mealPlanId")]
[QueryProperty(nameof(Day), "day")]
[QueryProperty(nameof(StartDate), "startDate")]
[QueryProperty(nameof(TotalDays), "totalDays")]
[QueryProperty(nameof(Mode), "mode")]
public partial class RecipePickerPage : ContentPage
{
    private readonly IRecipeService _recipeService;
    private readonly ObservableCollection<RecipePickerViewModel> _recipes = new();
    private readonly List<RecipePickerViewModel> _selectedRecipes = new();
    private bool _isMultiSelectMode;
    private int _maxSelections = 7;

    public string? MealPlanId { get; set; }
    public string? Day { get; set; }
    public string? StartDate { get; set; }
    public string? TotalDays { get; set; }
    public string? Mode { get; set; }

    public RecipePickerPage()
    {
        InitializeComponent();

        // Get service from DI
        _recipeService = Application.Current?.Handler?.MauiContext?.Services.GetService<IRecipeService>()
            ?? throw new InvalidOperationException("IRecipeService not registered");

        RecipesCollection.ItemsSource = _recipes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Determine mode based on query parameters
        _isMultiSelectMode = Mode?.Equals("multi", StringComparison.OrdinalIgnoreCase) == true;
        
        if (int.TryParse(TotalDays, out var totalDaysInt))
        {
            _maxSelections = totalDaysInt;
        }

        // Update UI based on mode
        if (_isMultiSelectMode)
        {
            HeaderLabel.Text = "Select Recipes";
            DayLabel.Text = $"Tap recipes in order to assign them to your meal plan (up to {_maxSelections})";
            ActionBar.IsVisible = true;
            UpdateSelectionCount();
        }
        else
        {
            // Single-select mode - update day label
            if (DateTime.TryParse(Day, out var dayDate))
            {
                HeaderLabel.Text = "Select a Recipe";
                DayLabel.Text = $"for {dayDate:dddd, MMMM d}";
            }
            ActionBar.IsVisible = false;
        }

        await LoadRecommendedRecipesAsync();
    }

    private async Task LoadRecommendedRecipesAsync()
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            RecipesCollection.IsVisible = false;
            EmptyState.IsVisible = false;

            // Get week start date from the day or startDate
            DateTime? weekStart = null;
            if (DateTime.TryParse(StartDate, out var startDate))
            {
                weekStart = startDate;
            }
            else if (DateTime.TryParse(Day, out var dayDate))
            {
                // Get the Monday of that week
                var daysFromMonday = ((int)dayDate.DayOfWeek - 1 + 7) % 7;
                weekStart = dayDate.AddDays(-daysFromMonday);
            }

            var response = await _recipeService.GetRecommendedRecipesAsync(weekStart);

            _recipes.Clear();
            _selectedRecipes.Clear();

            if (response?.Recipes != null && response.Recipes.Count > 0)
            {
                foreach (var recipe in response.Recipes)
                {
                    _recipes.Add(new RecipePickerViewModel(recipe));
                }
                RecipesCollection.IsVisible = true;
                EmptyState.IsVisible = false;
            }
            else
            {
                RecipesCollection.IsVisible = false;
                EmptyState.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load recipes: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnRecipeTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not RecipePickerViewModel recipe)
            return;

        if (_isMultiSelectMode)
        {
            HandleMultiSelectTap(recipe);
        }
        else
        {
            await HandleSingleSelectTap(recipe);
        }
    }

    private void HandleMultiSelectTap(RecipePickerViewModel recipe)
    {
        if (recipe.IsSelected)
        {
            // Deselect this recipe
            var order = recipe.SelectionOrder;
            recipe.IsSelected = false;
            recipe.SelectionOrder = 0;
            _selectedRecipes.Remove(recipe);

            // Update order numbers for remaining selections
            foreach (var r in _selectedRecipes.Where(r => r.SelectionOrder > order))
            {
                r.SelectionOrder--;
            }
        }
        else
        {
            // Select this recipe if we haven't reached max
            if (_selectedRecipes.Count >= _maxSelections)
            {
                DisplayAlert("Maximum Reached", $"You can only select up to {_maxSelections} recipes. Deselect one first.", "OK");
                return;
            }

            recipe.IsSelected = true;
            recipe.SelectionOrder = _selectedRecipes.Count + 1;
            _selectedRecipes.Add(recipe);
        }

        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        var count = _selectedRecipes.Count;
        SelectionCountLabel.Text = count == 1 ? "1 recipe selected" : $"{count} recipes selected";
        SelectionHintLabel.Text = count < _maxSelections 
            ? $"Select up to {_maxSelections - count} more" 
            : "Maximum selected";
        DoneButton.IsEnabled = count > 0;
    }

    private async Task HandleSingleSelectTap(RecipePickerViewModel recipe)
    {
        if (!Guid.TryParse(MealPlanId, out var mealPlanGuid))
        {
            await DisplayAlert("Error", "Invalid meal plan", "OK");
            return;
        }

        if (!DateTime.TryParse(Day, out var dayDate))
        {
            await DisplayAlert("Error", "Invalid date", "OK");
            return;
        }

        // Confirm selection
        var confirm = await DisplayAlert(
            "Add Recipe",
            $"Add \"{recipe.Title}\" to {dayDate:dddd, MMMM d}?",
            "Add",
            "Cancel");

        if (!confirm) return;

        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            var request = new AddRecipeToMealPlanDto
            {
                RecipeId = recipe.RecipeId,
                Day = dayDate
            };

            var result = await _recipeService.AddRecipeToMealPlanAsync(mealPlanGuid, request);

            if (result.Success)
            {
                // Go back to meal plan detail page
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add recipe", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add recipe: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        if (_selectedRecipes.Count == 0)
            return;

        if (!Guid.TryParse(MealPlanId, out var mealPlanGuid))
        {
            await DisplayAlert("Error", "Invalid meal plan", "OK");
            return;
        }

        if (!DateTime.TryParse(StartDate, out var startDate))
        {
            await DisplayAlert("Error", "Invalid start date", "OK");
            return;
        }

        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            DoneButton.IsEnabled = false;

            // Add recipes in selection order
            var orderedRecipes = _selectedRecipes.OrderBy(r => r.SelectionOrder).ToList();
            var currentDay = startDate;

            foreach (var recipe in orderedRecipes)
            {
                var request = new AddRecipeToMealPlanDto
                {
                    RecipeId = recipe.RecipeId,
                    Day = currentDay
                };

                var result = await _recipeService.AddRecipeToMealPlanAsync(mealPlanGuid, request);

                if (!result.Success)
                {
                    await DisplayAlert("Warning", $"Failed to add {recipe.Title}: {result.ErrorMessage}", "OK");
                }

                currentDay = currentDay.AddDays(1);
            }

            // Navigate to meal plan detail page
            await Shell.Current.GoToAsync($"../{nameof(MealPlanDetailPage)}?mealPlanId={MealPlanId}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add recipes: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            DoneButton.IsEnabled = true;
        }
    }
}

/// <summary>
/// ViewModel for displaying a recipe in the picker list.
/// </summary>
public class RecipePickerViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private int _selectionOrder;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid RecipeId { get; set; }
    public string Title { get; set; }
    public string? ImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public double Score { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public string? LastCookedDate { get; set; }
    public List<string> ReasonCodes { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotSelected));
                OnPropertyChanged(nameof(BorderColor));
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }
    }

    public bool IsNotSelected => !_isSelected;

    public int SelectionOrder
    {
        get => _selectionOrder;
        set
        {
            if (_selectionOrder != value)
            {
                _selectionOrder = value;
                OnPropertyChanged();
            }
        }
    }

    public string BorderColor => IsSelected ? "#4CAF50" : "#333333";
    public string BackgroundColor => IsSelected ? "#1B3D1B" : "Black";
    public string SelectionBadgeColor => "#4CAF50";

    public RecipePickerViewModel(RecommendedRecipeDto recipe)
    {
        RecipeId = recipe.RecipeId;
        Title = recipe.Title;
        ImageUrl = recipe.ImageUrl;
        CuisineType = recipe.CuisineType;
        PrepTimeMinutes = recipe.PrepTimeMinutes;
        CookTimeMinutes = recipe.CookTimeMinutes;
        Score = recipe.Score;
        AverageRating = recipe.AverageRating;
        RatingCount = recipe.RatingCount;
        LastCookedDate = recipe.LastCookedDate;
        ReasonCodes = recipe.ReasonCodes ?? new List<string>();
    }

    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
    public bool HasCuisine => !string.IsNullOrEmpty(CuisineType);
    public bool HasTime => PrepTimeMinutes.HasValue || CookTimeMinutes.HasValue;

    public string RatingDisplay
    {
        get
        {
            if (RatingCount == 0) return "No ratings";
            var stars = new string('★', (int)Math.Round(AverageRating));
            return $"{stars} ({RatingCount})";
        }
    }

    public string TimeDisplay
    {
        get
        {
            var total = (PrepTimeMinutes ?? 0) + (CookTimeMinutes ?? 0);
            return total > 0 ? $"{total} min" : "";
        }
    }

    public bool ShowScore => Score > 0;
    public string ScoreDisplay => $"{Score:F0}%";

    public bool HasReason => ReasonCodes.Count > 0;
    public string ReasonDisplay
    {
        get
        {
            var reasons = new List<string>();
            foreach (var code in ReasonCodes.Take(2))
            {
                var friendly = code switch
                {
                    "HighRated" => "Highly rated",
                    "FrequencyMatch" => "Matches your preferences",
                    "DueForRepeat" => "Ready for a repeat",
                    "NeverCooked" => "Try something new",
                    _ => code
                };
                reasons.Add(friendly);
            }
            return string.Join(" • ", reasons);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
