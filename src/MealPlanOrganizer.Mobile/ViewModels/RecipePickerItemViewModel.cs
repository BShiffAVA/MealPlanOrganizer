using CommunityToolkit.Mvvm.ComponentModel;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for displaying a single recipe item in the picker list.
/// </summary>
public partial class RecipePickerItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelected))]
    [NotifyPropertyChangedFor(nameof(BorderColor))]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    [NotifyPropertyChangedFor(nameof(ButtonBackgroundColor))]
    private bool _isSelected;

    [ObservableProperty]
    private int _selectionOrder;

    public Guid RecipeId { get; }
    public string Title { get; }
    public string? ImageUrl { get; }
    public string? CuisineType { get; }
    public int? PrepTimeMinutes { get; }
    public int? CookTimeMinutes { get; }
    public double Score { get; }
    public double AverageRating { get; }
    public int RatingCount { get; }
    public string? LastCookedDate { get; }
    public List<string> ReasonCodes { get; }

    public RecipePickerItemViewModel(RecommendedRecipeDto recipe)
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

    public bool IsNotSelected => !IsSelected;
    public string BorderColor => IsSelected ? "#4CAF50" : "#333333";
    public string BackgroundColor => IsSelected ? "#1B3D1B" : "Black";
    public string SelectionBadgeColor => "#4CAF50";
    
    // Button properties
    public string ButtonText => IsSelected ? "Remove" : "Add";
    public string ButtonBackgroundColor => IsSelected ? "#F44336" : "#4CAF50";

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
}
