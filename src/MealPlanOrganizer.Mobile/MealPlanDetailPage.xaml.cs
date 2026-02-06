using MealPlanOrganizer.Mobile.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MealPlanOrganizer.Mobile;

[QueryProperty(nameof(MealPlanId), "mealPlanId")]
public partial class MealPlanDetailPage : ContentPage
{
    private readonly IRecipeService _recipeService;
    private readonly ObservableCollection<MealPlanDayViewModel> _days = new();
    private MealPlanDetailDto? _mealPlan;
    private MealPlanDayViewModel? _draggedDay;
    private int _draggedFromIndex = -1;
    private int _currentHoverIndex = -1;
    private MealPlanDetailPageViewModel _viewModel;
    private bool _dropInProgress; // Prevents HandleDropCompleted from interfering

    public string? MealPlanId { get; set; }

    public MealPlanDetailPage()
    {
        InitializeComponent();

        // Get service from DI
        _recipeService = Application.Current?.Handler?.MauiContext?.Services.GetService<IRecipeService>()
            ?? throw new InvalidOperationException("IRecipeService not registered");

        _viewModel = new MealPlanDetailPageViewModel(this);
        BindingContext = _viewModel;
        DaysCollection.ItemsSource = _days;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(MealPlanId) && Guid.TryParse(MealPlanId, out _))
        {
            await LoadMealPlanAsync();
        }
    }

    private async Task LoadMealPlanAsync()
    {
        if (!Guid.TryParse(MealPlanId, out var mealPlanGuid))
        {
            await DisplayAlert("Error", "Invalid meal plan ID", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            DaysCollection.IsVisible = false;

            _mealPlan = await _recipeService.GetMealPlanAsync(mealPlanGuid);

            if (_mealPlan == null)
            {
                await DisplayAlert("Error", "Meal plan not found", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Update header
            PlanNameLabel.Text = _mealPlan.Name;
            StatusLabel.Text = _mealPlan.Status;
            DateRangeLabel.Text = $"{_mealPlan.StartDate} - {_mealPlan.EndDate}";

            // Update status badge color
            StatusBadge.BackgroundColor = _mealPlan.Status switch
            {
                "Active" => Color.FromArgb("#4CAF50"),
                "Complete" => Color.FromArgb("#2196F3"),
                "Draft" => Color.FromArgb("#9E9E9E"),
                _ => Color.FromArgb("#9E9E9E")
            };

            // Update progress
            var totalDays = _mealPlan.TotalDays;
            var assignedDays = _mealPlan.RecipesAssigned;
            ProgressLabel.Text = $"{assignedDays} of {totalDays} days planned";
            ProgressBar.Progress = totalDays > 0 ? (double)assignedDays / totalDays : 0;

            // Show drag-drop hint if there are recipes assigned
            DragDropHint.IsVisible = assignedDays > 0;

            // Populate days
            _days.Clear();
            foreach (var day in _mealPlan.Days)
            {
                _days.Add(new MealPlanDayViewModel(day));
            }

            DaysCollection.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load meal plan: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnDayActionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not MealPlanDayViewModel dayVm)
            return;

        if (!Guid.TryParse(MealPlanId, out var mealPlanGuid))
            return;

        // Navigate to recipe picker page
        await Shell.Current.GoToAsync(
            $"{nameof(RecipePickerPage)}?mealPlanId={mealPlanGuid}&day={dayVm.Date}");
    }

    #region Drag and Drop Handlers

    // Store original recipe positions for live preview
    private List<(int Index, MealPlanDayRecipeDto? Recipe)>? _originalRecipes;

    internal void HandleDragStarting(MealPlanDayViewModel day)
    {
        if (!day.HasRecipe) return;

        _draggedDay = day;
        _draggedFromIndex = _days.IndexOf(day);
        _currentHoverIndex = _draggedFromIndex;
        day.IsDragging = true;
        
        // Store original state for preview/reset
        _originalRecipes = _days.Select((d, i) => (i, d.Recipe)).ToList();
    }

    internal void HandleDropCompleted(MealPlanDayViewModel day)
    {
        // If a drop is in progress, don't interfere - HandleDrop will handle cleanup
        if (_dropInProgress) return;
        
        // Reset all visual states and restore original positions (drag was cancelled)
        RestoreOriginalPositions();
        
        foreach (var d in _days)
        {
            d.IsDragging = false;
            d.IsDropTarget = false;
        }
        _draggedDay = null;
        _draggedFromIndex = -1;
        _currentHoverIndex = -1;
        _originalRecipes = null;
    }

    internal void HandleDragOver(MealPlanDayViewModel targetDay)
    {
        if (_draggedDay == null || targetDay == _draggedDay) return;

        var targetIndex = _days.IndexOf(targetDay);
        if (targetIndex == _currentHoverIndex) return;

        // Reset previous drop target
        if (_currentHoverIndex >= 0 && _currentHoverIndex < _days.Count && _currentHoverIndex != _draggedFromIndex)
        {
            _days[_currentHoverIndex].IsDropTarget = false;
        }

        _currentHoverIndex = targetIndex;
        targetDay.IsDropTarget = true;

        // Live preview: shift recipes to show where the dragged item would go
        PreviewReorder(_draggedFromIndex, targetIndex);
    }

    internal void HandleDragLeave(MealPlanDayViewModel targetDay)
    {
        targetDay.IsDropTarget = false;
        
        // Only restore originals if we're not hovering over a different target
        // (DragLeave can fire after DragOver on new target due to async event timing)
        var leavingIndex = _days.IndexOf(targetDay);
        if (_currentHoverIndex == leavingIndex)
        {
            // We're leaving without entering a new target, restore originals
            RestoreOriginalPositions();
            _currentHoverIndex = _draggedFromIndex;
        }
        // If _currentHoverIndex != leavingIndex, we've already moved to a new target
        // so don't restore - the preview is already showing the new position
    }

    internal async Task HandleDrop(MealPlanDayViewModel targetDay)
    {
        if (_draggedDay == null) return;

        // Mark drop in progress to prevent HandleDropCompleted from interfering
        _dropInProgress = true;

        var sourceIndex = _draggedFromIndex;
        var targetIndex = _days.IndexOf(targetDay);

        // Reset all visual states
        foreach (var d in _days)
        {
            d.IsDragging = false;
            d.IsDropTarget = false;
        }

        if (sourceIndex == targetIndex || sourceIndex < 0 || targetIndex < 0)
        {
            RestoreOriginalPositions();
            _draggedDay = null;
            _draggedFromIndex = -1;
            _currentHoverIndex = -1;
            _originalRecipes = null;
            _dropInProgress = false;
            return;
        }

        if (!Guid.TryParse(MealPlanId, out var mealPlanGuid))
            return;

        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            // Perform the move/reorder on the server
            await MoveRecipeAsync(mealPlanGuid, sourceIndex, targetIndex);

            // Reload to get updated state
            await LoadMealPlanAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to move recipe: {ex.Message}", "OK");
            // Reload to reset state
            await LoadMealPlanAsync();
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            _draggedDay = null;
            _draggedFromIndex = -1;
            _currentHoverIndex = -1;
            _originalRecipes = null;
            _dropInProgress = false;
        }
    }

    private void RestoreOriginalPositions()
    {
        if (_originalRecipes == null) return;
        
        foreach (var (index, recipe) in _originalRecipes)
        {
            if (index < _days.Count)
            {
                _days[index].SetRecipeForPreview(recipe);
            }
        }
    }

    private void PreviewReorder(int fromIndex, int toIndex)
    {
        if (_originalRecipes == null || fromIndex == toIndex) return;
        
        // Create preview of what the order would look like after the drop
        var previewRecipes = _originalRecipes.ToDictionary(x => x.Index, x => x.Recipe);
        var movedRecipe = previewRecipes[fromIndex];
        var targetHasRecipe = previewRecipes[toIndex] != null;
        
        if (!targetHasRecipe)
        {
            // Simple move to empty slot - just show recipe in target, empty in source
            _days[fromIndex].SetRecipeForPreview(null);
            _days[toIndex].SetRecipeForPreview(movedRecipe);
        }
        else
        {
            // Need to shift recipes to make room
            // Remove from source position
            previewRecipes[fromIndex] = null;
            
            if (fromIndex < toIndex)
            {
                // Moving down: shift recipes between from+1 and to UP
                for (int i = fromIndex; i < toIndex; i++)
                {
                    previewRecipes[i] = _originalRecipes[i + 1].Recipe;
                }
                previewRecipes[toIndex] = movedRecipe;
            }
            else
            {
                // Moving up: shift recipes between to and from-1 DOWN  
                for (int i = fromIndex; i > toIndex; i--)
                {
                    previewRecipes[i] = _originalRecipes[i - 1].Recipe;
                }
                previewRecipes[toIndex] = movedRecipe;
            }
            
            // Apply preview
            foreach (var (index, recipe) in previewRecipes)
            {
                if (index < _days.Count)
                {
                    _days[index].SetRecipeForPreview(recipe);
                }
            }
        }
    }

    private async Task MoveRecipeAsync(Guid mealPlanId, int fromIndex, int toIndex)
    {
        // IMPORTANT: Use _originalRecipes, not _days[i].Recipe, because the preview 
        // has already modified the displayed values
        if (_originalRecipes == null) return;
        
        var sourceDay = _days[fromIndex];
        var targetDay = _days[toIndex];
        var sourceDate = DateTime.Parse(sourceDay.Date);
        var targetDate = DateTime.Parse(targetDay.Date);
        
        // Get the ORIGINAL recipe at source position (not the preview value)
        var movedRecipe = _originalRecipes.FirstOrDefault(r => r.Index == fromIndex).Recipe;
        if (movedRecipe == null) return;
        
        // Check if target ORIGINALLY had a recipe
        var originalTargetRecipe = _originalRecipes.FirstOrDefault(r => r.Index == toIndex).Recipe;
        var targetHasRecipe = originalTargetRecipe != null;
        
        if (!targetHasRecipe)
        {
            // Simple move to empty slot
            // 1. Add recipe to target day
            var addRequest = new AddRecipeToMealPlanDto
            {
                RecipeId = movedRecipe.RecipeId,
                Day = targetDate
            };
            await _recipeService.AddRecipeToMealPlanAsync(mealPlanId, addRequest);
            
            // 2. Remove from source day
            await _recipeService.RemoveRecipeFromMealPlanAsync(mealPlanId, sourceDate);
        }
        else
        {
            // Need to shift recipes to make room
            // Collect all recipes that need to be reassigned (using ORIGINAL positions)
            var recipesToReassign = new List<(DateTime TargetDate, Guid RecipeId)>();
            
            if (fromIndex < toIndex)
            {
                // Moving down: shift recipes between from+1 and to UP one slot
                for (int i = fromIndex + 1; i <= toIndex; i++)
                {
                    var originalRecipeAtI = _originalRecipes.FirstOrDefault(r => r.Index == i).Recipe;
                    if (originalRecipeAtI != null)
                    {
                        var prevDate = DateTime.Parse(_days[i - 1].Date);
                        recipesToReassign.Add((prevDate, originalRecipeAtI.RecipeId));
                    }
                }
                // The moved recipe goes to the target position
                recipesToReassign.Add((targetDate, movedRecipe.RecipeId));
            }
            else
            {
                // Moving up: shift recipes between to and from-1 DOWN one slot
                for (int i = fromIndex - 1; i >= toIndex; i--)
                {
                    var originalRecipeAtI = _originalRecipes.FirstOrDefault(r => r.Index == i).Recipe;
                    if (originalRecipeAtI != null)
                    {
                        var nextDate = DateTime.Parse(_days[i + 1].Date);
                        recipesToReassign.Add((nextDate, originalRecipeAtI.RecipeId));
                    }
                }
                // The moved recipe goes to the target position
                recipesToReassign.Add((targetDate, movedRecipe.RecipeId));
            }
            
            // Remove recipe from original source day first (if it won't be overwritten)
            bool sourceWillBeOverwritten = recipesToReassign.Any(r => r.TargetDate == sourceDate);
            if (!sourceWillBeOverwritten)
            {
                await _recipeService.RemoveRecipeFromMealPlanAsync(mealPlanId, sourceDate);
            }
            
            // Apply all reassignments
            foreach (var (date, recipeId) in recipesToReassign)
            {
                var request = new AddRecipeToMealPlanDto
                {
                    RecipeId = recipeId,
                    Day = date
                };
                await _recipeService.AddRecipeToMealPlanAsync(mealPlanId, request);
            }
        }
    }

    #endregion
}

/// <summary>
/// ViewModel for the MealPlanDetailPage to enable command binding.
/// </summary>
public class MealPlanDetailPageViewModel
{
    private readonly MealPlanDetailPage _page;

    public ICommand DragStartingCommand { get; }
    public ICommand DropCompletedCommand { get; }
    public ICommand DragOverCommand { get; }
    public ICommand DragLeaveCommand { get; }
    public ICommand DropCommand { get; }

    public MealPlanDetailPageViewModel(MealPlanDetailPage page)
    {
        _page = page;
        DragStartingCommand = new Command<MealPlanDayViewModel>(day => _page.HandleDragStarting(day));
        DropCompletedCommand = new Command<MealPlanDayViewModel>(day => _page.HandleDropCompleted(day));
        DragOverCommand = new Command<MealPlanDayViewModel>(day => _page.HandleDragOver(day));
        DragLeaveCommand = new Command<MealPlanDayViewModel>(day => _page.HandleDragLeave(day));
        DropCommand = new Command<MealPlanDayViewModel>(async day => await _page.HandleDrop(day));
    }
}

/// <summary>
/// ViewModel for displaying a day in the meal plan.
/// </summary>
public class MealPlanDayViewModel : INotifyPropertyChanged
{
    private bool _isDragging;
    private bool _isDropTarget;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Date { get; set; }
    public string DayOfWeek { get; set; }
    public MealPlanDayRecipeDto? Recipe { get; set; }

    public MealPlanDayViewModel(MealPlanDayDto day)
    {
        Date = day.Date;
        DayOfWeek = day.DayOfWeek.Length > 3 ? day.DayOfWeek[..3] : day.DayOfWeek;
        Recipe = day.Recipe;
    }

    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging != value)
            {
                _isDragging = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DragOpacity));
            }
        }
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (_isDropTarget != value)
            {
                _isDropTarget = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DropTargetBorderColor));
                OnPropertyChanged(nameof(DropTargetBackgroundColor));
            }
        }
    }

    public string DropTargetBorderColor => IsDropTarget ? "#4CAF50" : "#E0E0E0";
    public string DropTargetBackgroundColor => IsDropTarget ? "#E8F5E9" : "Transparent";
    public double DragOpacity => IsDragging ? 0.5 : 1.0;

    public bool HasRecipe => Recipe != null;
    public bool HasRecipeImage => !string.IsNullOrEmpty(Recipe?.RecipeImageUrl);
    public string RecipeImageUrl => Recipe?.RecipeImageUrl ?? "";
    public string RecipeTitle => Recipe?.RecipeTitle ?? "Tap to add a recipe";
    
    public string RecipeDetails
    {
        get
        {
            if (Recipe == null) return "";
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Recipe.CuisineType))
                parts.Add(Recipe.CuisineType);
            if (Recipe.PrepTimeMinutes.HasValue)
                parts.Add($"{Recipe.PrepTimeMinutes + (Recipe.CookTimeMinutes ?? 0)} min");
            return string.Join(" • ", parts);
        }
    }

    public string DisplayDate
    {
        get
        {
            if (DateTime.TryParse(Date, out var dt))
                return dt.ToString("M/d");
            return Date;
        }
    }

    public string ActionButtonText => HasRecipe ? "Change" : "Add";

    /// <summary>
    /// Sets the recipe for preview purposes during drag operations.
    /// Updates the Recipe property and notifies all dependent properties.
    /// </summary>
    public void SetRecipeForPreview(MealPlanDayRecipeDto? recipe)
    {
        Recipe = recipe;
        OnPropertyChanged(nameof(Recipe));
        OnPropertyChanged(nameof(HasRecipe));
        OnPropertyChanged(nameof(HasRecipeImage));
        OnPropertyChanged(nameof(RecipeImageUrl));
        OnPropertyChanged(nameof(RecipeTitle));
        OnPropertyChanged(nameof(RecipeDetails));
        OnPropertyChanged(nameof(ActionButtonText));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Converter for day background based on whether recipe is assigned.
/// </summary>
public class HasRecipeBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var hasRecipe = value is bool b && b;
        return hasRecipe 
            ? Color.FromArgb("#E8F5E9") // Light green
            : Color.FromArgb("#FFF3E0"); // Light orange
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for action button color based on whether recipe is assigned.
/// </summary>
public class ActionButtonColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var hasRecipe = value is bool b && b;
        return hasRecipe 
            ? Color.FromArgb("#FF9800") // Orange for change
            : Color.FromArgb("#4CAF50"); // Green for add
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
