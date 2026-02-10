using MealPlanOrganizer.Mobile.Services;
using MealPlanOrganizer.Mobile.ViewModels;
using System.Windows.Input;

namespace MealPlanOrganizer.Mobile;

[QueryProperty(nameof(MealPlanId), "mealPlanId")]
public partial class MealPlanDetailPage : ContentPage
{
    private MealPlanDayViewModel? _draggedDay;
    private int _draggedFromIndex = -1;
    private int _currentHoverIndex = -1;
    private bool _dropInProgress;

    // Store original recipe positions for live preview
    private List<(int Index, MealPlanDayRecipeDto? Recipe)>? _originalRecipes;

    public string? MealPlanId { get; set; }

    public MealPlanDetailViewModel ViewModel { get; }

    // Commands for drag-drop (exposed as public properties for XAML binding)
    public ICommand DragStartingCommand { get; }
    public ICommand DropCompletedCommand { get; }
    public ICommand DragOverCommand { get; }
    public ICommand DragLeaveCommand { get; }
    public ICommand DropCommand { get; }

    public MealPlanDetailPage(MealPlanDetailViewModel viewModel)
    {
        InitializeComponent();

        ViewModel = viewModel;
        BindingContext = this;

        // Initialize drag-drop commands
        DragStartingCommand = new Command<MealPlanDayViewModel>(HandleDragStarting);
        DropCompletedCommand = new Command<MealPlanDayViewModel>(HandleDropCompleted);
        DragOverCommand = new Command<MealPlanDayViewModel>(HandleDragOver);
        DragLeaveCommand = new Command<MealPlanDayViewModel>(HandleDragLeave);
        DropCommand = new Command<MealPlanDayViewModel>(async day => await HandleDrop(day));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(MealPlanId))
        {
            await ViewModel.LoadCommand.ExecuteAsync(MealPlanId);
        }
    }

    private async void OnDayActionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not MealPlanDayViewModel dayVm)
            return;

        await ViewModel.DayActionCommand.ExecuteAsync(dayVm);
    }

    #region Drag and Drop Handlers

    private void HandleDragStarting(MealPlanDayViewModel day)
    {
        if (!day.HasRecipe) return;

        _draggedDay = day;
        _draggedFromIndex = ViewModel.Days.IndexOf(day);
        _currentHoverIndex = _draggedFromIndex;
        day.IsDragging = true;
        
        // Store original state for preview/reset
        _originalRecipes = ViewModel.Days.Select((d, i) => (i, d.Recipe)).ToList();
    }

    private void HandleDropCompleted(MealPlanDayViewModel day)
    {
        // If a drop is in progress, don't interfere - HandleDrop will handle cleanup
        if (_dropInProgress) return;
        
        // Reset all visual states and restore original positions (drag was cancelled)
        RestoreOriginalPositions();
        
        foreach (var d in ViewModel.Days)
        {
            d.IsDragging = false;
            d.IsDropTarget = false;
        }
        _draggedDay = null;
        _draggedFromIndex = -1;
        _currentHoverIndex = -1;
        _originalRecipes = null;
    }

    private void HandleDragOver(MealPlanDayViewModel targetDay)
    {
        if (_draggedDay == null || targetDay == _draggedDay) return;

        var targetIndex = ViewModel.Days.IndexOf(targetDay);
        if (targetIndex == _currentHoverIndex) return;

        // Reset previous drop target
        if (_currentHoverIndex >= 0 && _currentHoverIndex < ViewModel.Days.Count && _currentHoverIndex != _draggedFromIndex)
        {
            ViewModel.Days[_currentHoverIndex].IsDropTarget = false;
        }

        _currentHoverIndex = targetIndex;
        targetDay.IsDropTarget = true;

        // Live preview: shift recipes to show where the dragged item would go
        PreviewReorder(_draggedFromIndex, targetIndex);
    }

    private void HandleDragLeave(MealPlanDayViewModel targetDay)
    {
        targetDay.IsDropTarget = false;
        
        // Only restore originals if we're not hovering over a different target
        var leavingIndex = ViewModel.Days.IndexOf(targetDay);
        if (_currentHoverIndex == leavingIndex)
        {
            RestoreOriginalPositions();
            _currentHoverIndex = _draggedFromIndex;
        }
    }

    private async Task HandleDrop(MealPlanDayViewModel targetDay)
    {
        if (_draggedDay == null) return;

        // Mark drop in progress to prevent HandleDropCompleted from interfering
        _dropInProgress = true;

        var sourceIndex = _draggedFromIndex;
        var targetIndex = ViewModel.Days.IndexOf(targetDay);

        // Reset all visual states
        foreach (var d in ViewModel.Days)
        {
            d.IsDragging = false;
            d.IsDropTarget = false;
        }

        if (sourceIndex == targetIndex || sourceIndex < 0 || targetIndex < 0)
        {
            RestoreOriginalPositions();
            ResetDragState();
            return;
        }

        try
        {
            ViewModel.IsLoading = true;

            // Perform the move on the server
            var fromDay = ViewModel.Days[sourceIndex];
            var toDay = ViewModel.Days[targetIndex];
            var success = await MoveRecipeAsync(sourceIndex, targetIndex);

            if (!success)
            {
                await DisplayAlert("Error", "Failed to move recipe", "OK");
            }

            // Reload to get updated state
            await ViewModel.LoadMealPlanCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to move recipe: {ex.Message}", "OK");
            await ViewModel.LoadMealPlanCommand.ExecuteAsync(null);
        }
        finally
        {
            ResetDragState();
        }
    }

    private void ResetDragState()
    {
        _draggedDay = null;
        _draggedFromIndex = -1;
        _currentHoverIndex = -1;
        _originalRecipes = null;
        _dropInProgress = false;
    }

    private void RestoreOriginalPositions()
    {
        if (_originalRecipes == null) return;
        
        foreach (var (index, recipe) in _originalRecipes)
        {
            if (index < ViewModel.Days.Count)
            {
                ViewModel.Days[index].SetRecipeForPreview(recipe);
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
            // Simple move to empty slot
            ViewModel.Days[fromIndex].SetRecipeForPreview(null);
            ViewModel.Days[toIndex].SetRecipeForPreview(movedRecipe);
        }
        else
        {
            // Need to shift recipes to make room
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
                if (index < ViewModel.Days.Count)
                {
                    ViewModel.Days[index].SetRecipeForPreview(recipe);
                }
            }
        }
    }

    private async Task<bool> MoveRecipeAsync(int fromIndex, int toIndex)
    {
        if (_originalRecipes == null || ViewModel.MealPlanId == Guid.Empty) 
            return false;

        var recipeService = Handler?.MauiContext?.Services.GetService<IRecipeService>();
        if (recipeService == null) return false;
        
        var mealPlanId = ViewModel.MealPlanId;
        var sourceDay = ViewModel.Days[fromIndex];
        var targetDay = ViewModel.Days[toIndex];
        var sourceDate = DateTime.Parse(sourceDay.Date);
        var targetDate = DateTime.Parse(targetDay.Date);
        
        // Get the ORIGINAL recipe at source position
        var movedRecipe = _originalRecipes.FirstOrDefault(r => r.Index == fromIndex).Recipe;
        if (movedRecipe == null) return false;
        
        // Check if target ORIGINALLY had a recipe
        var originalTargetRecipe = _originalRecipes.FirstOrDefault(r => r.Index == toIndex).Recipe;
        var targetHasRecipe = originalTargetRecipe != null;
        
        if (!targetHasRecipe)
        {
            // Simple move to empty slot
            var addRequest = new AddRecipeToMealPlanDto
            {
                RecipeId = movedRecipe.RecipeId,
                Day = targetDate
            };
            await recipeService.AddRecipeToMealPlanAsync(mealPlanId, addRequest);
            await recipeService.RemoveRecipeFromMealPlanAsync(mealPlanId, sourceDate);
        }
        else
        {
            // Need to shift recipes to make room
            var recipesToReassign = new List<(DateTime TargetDate, Guid RecipeId)>();
            
            if (fromIndex < toIndex)
            {
                // Moving down: shift recipes between from+1 and to UP one slot
                for (int i = fromIndex + 1; i <= toIndex; i++)
                {
                    var originalRecipeAtI = _originalRecipes.FirstOrDefault(r => r.Index == i).Recipe;
                    if (originalRecipeAtI != null)
                    {
                        var prevDate = DateTime.Parse(ViewModel.Days[i - 1].Date);
                        recipesToReassign.Add((prevDate, originalRecipeAtI.RecipeId));
                    }
                }
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
                        var nextDate = DateTime.Parse(ViewModel.Days[i + 1].Date);
                        recipesToReassign.Add((nextDate, originalRecipeAtI.RecipeId));
                    }
                }
                recipesToReassign.Add((targetDate, movedRecipe.RecipeId));
            }
            
            // Remove recipe from original source day first (if it won't be overwritten)
            bool sourceWillBeOverwritten = recipesToReassign.Any(r => r.TargetDate == sourceDate);
            if (!sourceWillBeOverwritten)
            {
                await recipeService.RemoveRecipeFromMealPlanAsync(mealPlanId, sourceDate);
            }
            
            // Apply all reassignments
            foreach (var (date, recipeId) in recipesToReassign)
            {
                var request = new AddRecipeToMealPlanDto
                {
                    RecipeId = recipeId,
                    Day = date
                };
                await recipeService.AddRecipeToMealPlanAsync(mealPlanId, request);
            }
        }

        return true;
    }

    #endregion
}
