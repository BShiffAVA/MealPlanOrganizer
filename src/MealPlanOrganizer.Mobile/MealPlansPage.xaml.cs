using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class MealPlansPage : ContentPage
{
    private readonly MealPlansViewModel _viewModel;
    
    public MealPlansPage(MealPlansViewModel viewModel)
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
