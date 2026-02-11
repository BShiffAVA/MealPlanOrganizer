using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class JoinHouseholdPage : ContentPage
{
    private readonly JoinHouseholdViewModel _viewModel;

    public JoinHouseholdPage(JoinHouseholdViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
