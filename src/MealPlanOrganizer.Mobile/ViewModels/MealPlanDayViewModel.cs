using CommunityToolkit.Mvvm.ComponentModel;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for displaying a day in the meal plan.
/// </summary>
public partial class MealPlanDayViewModel : ObservableObject
{
    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _dayOfWeek = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecipe))]
    [NotifyPropertyChangedFor(nameof(HasRecipeImage))]
    [NotifyPropertyChangedFor(nameof(RecipeImageUrl))]
    [NotifyPropertyChangedFor(nameof(RecipeTitle))]
    [NotifyPropertyChangedFor(nameof(RecipeDetails))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    private MealPlanDayRecipeDto? _recipe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DragOpacity))]
    private bool _isDragging;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DropTargetBorderColor))]
    [NotifyPropertyChangedFor(nameof(DropTargetBackgroundColor))]
    private bool _isDropTarget;

    public MealPlanDayViewModel()
    {
    }

    public MealPlanDayViewModel(MealPlanDayDto day)
    {
        Date = day.Date;
        DayOfWeek = day.DayOfWeek.Length > 3 ? day.DayOfWeek[..3] : day.DayOfWeek;
        Recipe = day.Recipe;
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
    }
}
