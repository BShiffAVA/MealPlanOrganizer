using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the MealPlansPage.
/// </summary>
public partial class MealPlansViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<MealPlanDto> _mealPlans = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _errorMessage;

    public MealPlansViewModel(IRecipeService recipeService, INavigationService navigationService)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
    }

    /// <summary>
    /// Loads the list of meal plans from the API.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var response = await _recipeService.GetMealPlansAsync();

            MealPlans.Clear();

            if (response?.MealPlans != null && response.MealPlans.Count > 0)
            {
                foreach (var plan in response.MealPlans)
                {
                    MealPlans.Add(plan);
                }
                IsEmpty = false;
            }
            else
            {
                IsEmpty = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load meal plans: {ex.Message}";
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Navigates to the create meal plan page.
    /// </summary>
    [RelayCommand]
    private async Task CreateMealPlanAsync()
    {
        await _navigationService.GoToAsync(nameof(CreateMealPlanPage));
    }

    /// <summary>
    /// Navigates to the meal plan detail page.
    /// </summary>
    [RelayCommand]
    private async Task ViewMealPlanAsync(MealPlanDto mealPlan)
    {
        if (mealPlan != null)
        {
            await _navigationService.GoToAsync($"{nameof(MealPlanDetailPage)}?mealPlanId={mealPlan.Id}");
        }
    }
}
