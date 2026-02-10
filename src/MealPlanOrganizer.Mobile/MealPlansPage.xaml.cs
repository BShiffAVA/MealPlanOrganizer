using MealPlanOrganizer.Mobile.Services;
using System.Collections.ObjectModel;

namespace MealPlanOrganizer.Mobile;

public partial class MealPlansPage : ContentPage
{
    private readonly IRecipeService _recipeService;
    private readonly ObservableCollection<MealPlanDto> _mealPlans = new();
    
    public MealPlansPage()
    {
        InitializeComponent();
        
        // Get service from DI
        _recipeService = Application.Current?.Handler?.MauiContext?.Services.GetService<IRecipeService>()
            ?? throw new InvalidOperationException("IRecipeService not registered");
        
        MealPlansCollection.ItemsSource = _mealPlans;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMealPlansAsync();
    }

    private async Task LoadMealPlansAsync()
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            MealPlansCollection.IsVisible = false;
            EmptyState.IsVisible = false;

            var response = await _recipeService.GetMealPlansAsync();

            _mealPlans.Clear();

            if (response?.MealPlans != null && response.MealPlans.Count > 0)
            {
                foreach (var plan in response.MealPlans)
                {
                    _mealPlans.Add(plan);
                }
                MealPlansCollection.IsVisible = true;
                EmptyState.IsVisible = false;
            }
            else
            {
                MealPlansCollection.IsVisible = false;
                EmptyState.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load meal plans: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnCreateMealPlanClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CreateMealPlanPage));
    }

    private async void OnMealPlanTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Guid mealPlanId)
        {
            await Shell.Current.GoToAsync($"{nameof(MealPlanDetailPage)}?mealPlanId={mealPlanId}");
        }
    }
}
