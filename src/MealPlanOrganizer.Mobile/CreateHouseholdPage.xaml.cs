using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class CreateHouseholdPage : ContentPage
{
    private readonly CreateHouseholdViewModel _viewModel;

    public CreateHouseholdPage(CreateHouseholdViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
