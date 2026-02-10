using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class RecipePickerPage : ContentPage
{
    private readonly RecipePickerPageViewModel _viewModel;

    public RecipePickerPage(RecipePickerPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Update empty state visibility based on recipes
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RecipePickerPageViewModel.ShowEmptyState)
                || e.PropertyName == nameof(RecipePickerPageViewModel.IsLoading))
            {
                UpdateEmptyStateVisibility();
            }
        };

        UpdateEmptyStateVisibility();
    }

    private void UpdateEmptyStateVisibility()
    {
        // Show empty state only when not loading and no recipes
        EmptyState.IsVisible = _viewModel.ShowEmptyState && !_viewModel.IsLoading;
        RecipesCollection.IsVisible = !_viewModel.ShowEmptyState && !_viewModel.IsLoading;
    }
}
