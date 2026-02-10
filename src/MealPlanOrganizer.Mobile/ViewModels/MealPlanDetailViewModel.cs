using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for MealPlanDetailPage. Handles data loading and state management.
/// Note: Drag-drop visual logic remains in code-behind due to MAUI gesture limitations.
/// </summary>
public partial class MealPlanDetailViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private Guid _mealPlanId;

    [ObservableProperty]
    private MealPlanDetailDto? _mealPlan;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _planName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private Color _statusColor = Color.FromArgb("#9E9E9E");

    [ObservableProperty]
    private string _dateRange = string.Empty;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _showDragDropHint;

    [ObservableProperty]
    private ObservableCollection<MealPlanDayViewModel> _days = new();

    public MealPlanDetailViewModel(
        IRecipeService recipeService,
        INavigationService navigationService)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    public async Task LoadAsync(string mealPlanIdStr)
    {
        if (!Guid.TryParse(mealPlanIdStr, out var mealPlanGuid))
        {
            ErrorMessage = "Invalid meal plan ID";
            return;
        }

        MealPlanId = mealPlanGuid;
        await LoadMealPlanAsync();
    }

    [RelayCommand]
    public async Task LoadMealPlanAsync()
    {
        if (MealPlanId == Guid.Empty) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var mealPlan = await _recipeService.GetMealPlanAsync(MealPlanId);

            if (mealPlan == null)
            {
                ErrorMessage = "Meal plan not found";
                return;
            }

            MealPlan = mealPlan;
            PopulateFromMealPlan(mealPlan);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load meal plan {MealPlanId}: {ex.Message}");
            ErrorMessage = $"Failed to load meal plan: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateFromMealPlan(MealPlanDetailDto mealPlan)
    {
        PlanName = mealPlan.Name;
        Status = mealPlan.Status;
        DateRange = $"{mealPlan.StartDate} - {mealPlan.EndDate}";

        StatusColor = mealPlan.Status switch
        {
            "Active" => Color.FromArgb("#4CAF50"),
            "Complete" => Color.FromArgb("#2196F3"),
            "Draft" => Color.FromArgb("#9E9E9E"),
            _ => Color.FromArgb("#9E9E9E")
        };

        var totalDays = mealPlan.TotalDays;
        var assignedDays = mealPlan.RecipesAssigned;
        ProgressText = $"{assignedDays} of {totalDays} days planned";
        ProgressValue = totalDays > 0 ? (double)assignedDays / totalDays : 0;

        ShowDragDropHint = assignedDays > 0;

        Days.Clear();
        foreach (var day in mealPlan.Days)
        {
            Days.Add(new MealPlanDayViewModel(day));
        }
    }

    [RelayCommand]
    private async Task DayActionAsync(MealPlanDayViewModel day)
    {
        if (MealPlanId == Guid.Empty) return;

        // Navigate to recipe picker page
        await _navigationService.GoToAsync(
            $"{nameof(RecipePickerPage)}?mealPlanId={MealPlanId}&day={day.Date}");
    }

    /// <summary>
    /// Moves a recipe between days in the meal plan.
    /// Called from code-behind after drag-drop completes.
    /// </summary>
    public async Task<bool> MoveRecipeAsync(MealPlanDayViewModel fromDay, MealPlanDayViewModel toDay)
    {
        if (fromDay.Recipe == null || MealPlanId == Guid.Empty)
            return false;

        try
        {
            var fromIndex = Days.IndexOf(fromDay);
            var toIndex = Days.IndexOf(toDay);

            if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
                return false;

            var mealPlanId = MealPlanId;
            var movedRecipe = fromDay.Recipe;
            var sourceDate = DateTime.Parse(fromDay.Date);
            var targetDate = DateTime.Parse(toDay.Date);

            // Build list of recipes to reassign (shift all between source and target)
            var recipesToReassign = new List<(DateTime TargetDate, Guid RecipeId)>();
            
            // Store the original recipes at each position
            var originalRecipes = Days.Select((d, i) => (Index: i, Recipe: d.Recipe)).ToList();

            if (fromIndex < toIndex)
            {
                // Moving down: shift recipes up
                for (int i = fromIndex + 1; i <= toIndex; i++)
                {
                    var originalRecipeAtI = originalRecipes.FirstOrDefault(r => r.Index == i).Recipe;
                    if (originalRecipeAtI != null)
                    {
                        var prevDate = DateTime.Parse(Days[i - 1].Date);
                        recipesToReassign.Add((prevDate, originalRecipeAtI.RecipeId));
                    }
                }
                recipesToReassign.Add((targetDate, movedRecipe.RecipeId));
            }
            else
            {
                // Moving up: shift recipes down
                for (int i = fromIndex - 1; i >= toIndex; i--)
                {
                    var originalRecipeAtI = originalRecipes.FirstOrDefault(r => r.Index == i).Recipe;
                    if (originalRecipeAtI != null)
                    {
                        var nextDate = DateTime.Parse(Days[i + 1].Date);
                        recipesToReassign.Add((nextDate, originalRecipeAtI.RecipeId));
                    }
                }
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

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to move recipe: {ex.Message}");
            return false;
        }
    }
}
