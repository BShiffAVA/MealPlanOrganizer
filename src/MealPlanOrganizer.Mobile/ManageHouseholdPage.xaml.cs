namespace MealPlanOrganizer.Mobile;

using MealPlanOrganizer.Mobile.ViewModels;

public partial class ManageHouseholdPage : ContentPage
{
    private readonly ManageHouseholdViewModel _viewModel;

    public ManageHouseholdPage(ManageHouseholdViewModel viewModel)
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
