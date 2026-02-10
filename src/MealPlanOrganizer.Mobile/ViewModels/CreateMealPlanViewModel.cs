using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the Create Meal Plan page.
/// </summary>
public partial class CreateMealPlanViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly ILogger<CreateMealPlanViewModel> _logger;

    [ObservableProperty]
    private string? _planName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(DaysCount))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(DaysCount))]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private DateTime _endDate = DateTime.Today.AddDays(6);

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    public DateTime Today => DateTime.Today;

    public int DaysCount => Math.Max(0, (EndDate - StartDate).Days + 1);

    public string SummaryText
    {
        get
        {
            var days = DaysCount;
            if (days < 1)
            {
                return "End date must be after start date";
            }
            return $"{days} dinner{(days == 1 ? "" : "s")} from {StartDate:ddd, MMM d} to {EndDate:ddd, MMM d}";
        }
    }

    public bool SummaryIsError => DaysCount < 1;

    public bool CanCreate => DaysCount > 0 && DaysCount <= 31 && !string.IsNullOrWhiteSpace(PlanName);

    public CreateMealPlanViewModel(
        IRecipeService recipeService,
        ILogger<CreateMealPlanViewModel> logger)
    {
        _recipeService = recipeService;
        _logger = logger;

        // Initialize with next week by default
        SetNextWeek();
    }

    partial void OnStartDateChanged(DateTime value)
    {
        // Ensure end date is not before start date
        if (EndDate < value)
        {
            EndDate = value.AddDays(6);
        }

        // Auto-update plan name if it still matches "Week of" or "Two Weeks from" pattern
        var currentName = PlanName?.Trim() ?? "";
        if (currentName.StartsWith("Week of ") || currentName.StartsWith("Two Weeks from ") || string.IsNullOrEmpty(currentName))
        {
            PlanName = $"Week of {value:MMMM d}";
        }

        OnPropertyChanged(nameof(SummaryIsError));
        OnPropertyChanged(nameof(CanCreate));
    }

    partial void OnEndDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(SummaryIsError));
        OnPropertyChanged(nameof(CanCreate));
    }

    partial void OnPlanNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CanCreate));
    }

    [RelayCommand]
    private void SelectThisWeek()
    {
        var today = DateTime.Today;
        var monday = today.AddDays(-((int)today.DayOfWeek - 1 + 7) % 7);
        if (monday < today) monday = today;

        var sunday = monday.AddDays(6);

        StartDate = monday;
        EndDate = sunday;
        PlanName = $"Week of {monday:MMMM d}";
    }

    [RelayCommand]
    private void SelectNextWeek()
    {
        SetNextWeek();
    }

    private void SetNextWeek()
    {
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;

        var nextMonday = today.AddDays(daysUntilMonday);
        var nextSunday = nextMonday.AddDays(6);

        StartDate = nextMonday;
        EndDate = nextSunday;
        PlanName = $"Week of {nextMonday:MMMM d}";
    }

    [RelayCommand]
    private void SelectNext2Weeks()
    {
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;

        var nextMonday = today.AddDays(daysUntilMonday);
        var twoWeeksSunday = nextMonday.AddDays(13);

        StartDate = nextMonday;
        EndDate = twoWeeksSunday;
        PlanName = $"Two Weeks from {nextMonday:MMMM d}";
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        HasError = false;
        ErrorMessage = null;

        // Validate inputs
        var name = PlanName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Please enter a name for your meal plan.";
            HasError = true;
            return;
        }

        if (EndDate < StartDate)
        {
            ErrorMessage = "End date must be after start date.";
            HasError = true;
            return;
        }

        var days = DaysCount;
        if (days > 31)
        {
            ErrorMessage = "Meal plans can be at most 31 days.";
            HasError = true;
            return;
        }

        try
        {
            IsLoading = true;
            _logger.LogInformation("Creating meal plan: {Name}, {StartDate} to {EndDate}", name, StartDate, EndDate);

            var request = new CreateMealPlanDto
            {
                Name = name,
                StartDate = StartDate,
                EndDate = EndDate
            };

            var result = await _recipeService.CreateMealPlanAsync(request);

            if (result.Success && result.MealPlanId.HasValue)
            {
                _logger.LogInformation("Meal plan created: {MealPlanId}", result.MealPlanId);

                // Navigate to recipe picker page in multi-select mode
                var startDateStr = StartDate.ToString("o");
                await Shell.Current.GoToAsync(
                    $"../{nameof(RecipePickerPage)}?mealPlanId={result.MealPlanId}&startDate={Uri.EscapeDataString(startDateStr)}&totalDays={days}&mode=multi");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to create meal plan.";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create meal plan");
            ErrorMessage = $"Error: {ex.Message}";
            HasError = true;
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
