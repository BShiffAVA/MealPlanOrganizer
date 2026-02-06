using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class CreateMealPlanPage : ContentPage
{
    private readonly IRecipeService _recipeService;
    
    public DateTime Today => DateTime.Today;
    
    public CreateMealPlanPage()
    {
        InitializeComponent();
        BindingContext = this;
        
        // Get service from DI
        _recipeService = Application.Current?.Handler?.MauiContext?.Services.GetService<IRecipeService>()
            ?? throw new InvalidOperationException("IRecipeService not registered");
        
        // Initialize with next week by default
        SetNextWeek();
    }

    private void SetThisWeek()
    {
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var monday = today.AddDays(-((int)today.DayOfWeek - 1 + 7) % 7); // This week's Monday
        if (monday < today) monday = today; // Don't go before today
        
        var sunday = monday.AddDays(6);
        
        StartDatePicker.Date = monday;
        EndDatePicker.Date = sunday;
        NameEntry.Text = $"Week of {monday:MMMM d}";
        UpdateSummary();
    }

    private void SetNextWeek()
    {
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7; // If today is Monday, get next Monday
        
        var nextMonday = today.AddDays(daysUntilMonday);
        var nextSunday = nextMonday.AddDays(6);
        
        StartDatePicker.Date = nextMonday;
        EndDatePicker.Date = nextSunday;
        NameEntry.Text = $"Week of {nextMonday:MMMM d}";
        UpdateSummary();
    }

    private void SetNext2Weeks()
    {
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        
        var nextMonday = today.AddDays(daysUntilMonday);
        var twoWeeksSunday = nextMonday.AddDays(13);
        
        StartDatePicker.Date = nextMonday;
        EndDatePicker.Date = twoWeeksSunday;
        NameEntry.Text = $"Two Weeks from {nextMonday:MMMM d}";
        UpdateSummary();
    }

    private void OnThisWeekClicked(object? sender, EventArgs e)
    {
        SetThisWeek();
    }

    private void OnNextWeekClicked(object? sender, EventArgs e)
    {
        SetNextWeek();
    }

    private void OnNext2WeeksClicked(object? sender, EventArgs e)
    {
        SetNext2Weeks();
    }

    private void OnStartDateSelected(object? sender, DateChangedEventArgs e)
    {
        // Ensure end date is not before start date
        var startDate = StartDatePicker.Date ?? DateTime.Today;
        var endDate = EndDatePicker.Date ?? DateTime.Today;
        if (endDate < startDate)
        {
            EndDatePicker.Date = startDate.AddDays(6);
        }
        
        // Auto-update plan name if it still matches "Week of" or "Two Weeks from" pattern
        var currentName = NameEntry.Text?.Trim() ?? "";
        if (currentName.StartsWith("Week of ") || currentName.StartsWith("Two Weeks from "))
        {
            NameEntry.Text = $"Week of {startDate:MMMM d}";
        }
        
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var startDate = StartDatePicker.Date ?? DateTime.Today;
        var endDate = EndDatePicker.Date ?? DateTime.Today;
        var days = (endDate - startDate).Days + 1;
        
        if (days < 1)
        {
            SummaryLabel.Text = "End date must be after start date";
            SummaryLabel.TextColor = Colors.Red;
        }
        else
        {
            SummaryLabel.Text = $"{days} dinner{(days == 1 ? "" : "s")} from {startDate:ddd, MMM d} to {endDate:ddd, MMM d}";
            SummaryLabel.TextColor = Colors.Gray;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        // Validate inputs
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorLabel.Text = "Please enter a name for your meal plan.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var startDate = StartDatePicker.Date ?? DateTime.Today;
        var endDate = EndDatePicker.Date ?? DateTime.Today;

        if (endDate < startDate)
        {
            ErrorLabel.Text = "End date must be after start date.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var days = (endDate - startDate).Days + 1;
        if (days > 31)
        {
            ErrorLabel.Text = "Meal plans can be at most 31 days.";
            ErrorLabel.IsVisible = true;
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            var request = new CreateMealPlanDto
            {
                Name = name,
                StartDate = startDate,
                EndDate = endDate
            };

            var result = await _recipeService.CreateMealPlanAsync(request);

            if (result.Success && result.MealPlanId.HasValue)
            {
                // Navigate to recipe picker page in multi-select mode
                var startDateStr = startDate.ToString("o");
                await Shell.Current.GoToAsync(
                    $"../{nameof(RecipePickerPage)}?mealPlanId={result.MealPlanId}&startDate={Uri.EscapeDataString(startDateStr)}&totalDays={days}&mode=multi");
            }
            else
            {
                ErrorLabel.Text = result.ErrorMessage ?? "Failed to create meal plan.";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Error: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            CreateButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }
}
