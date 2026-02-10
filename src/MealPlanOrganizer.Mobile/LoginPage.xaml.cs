using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CheckAuthenticationCommand.ExecuteAsync(null);
    }
}

