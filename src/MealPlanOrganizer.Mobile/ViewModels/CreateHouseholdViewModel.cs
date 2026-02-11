using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the CreateHouseholdPage.
/// Handles household creation for new users.
/// </summary>
public partial class CreateHouseholdViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _householdName = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private string _userDisplayName = string.Empty;

    public CreateHouseholdViewModel(IUserService userService, IAuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }
    
    /// <summary>
    /// Loads initial data including the user's display name.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            UserDisplayName = await _authService.GetUserDisplayNameAsync() ?? "there";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading user name: {ex.Message}");
            UserDisplayName = "there";
        }
    }

    /// <summary>
    /// Creates the household and navigates to the main page.
    /// </summary>
    [RelayCommand]
    private async Task CreateHouseholdAsync()
    {
        if (string.IsNullOrWhiteSpace(HouseholdName))
        {
            ShowError("Please enter a name for your household.");
            return;
        }

        try
        {
            IsLoading = true;
            IsErrorVisible = false;

            var household = await _userService.CreateHouseholdAsync(HouseholdName.Trim());

            if (household != null)
            {
                System.Diagnostics.Debug.WriteLine($"Household created: {household.Name} ({household.Id})");
                NavigateToMainPage();
            }
            else
            {
                ShowError("Failed to create household. Please try again.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating household: {ex.Message}");
            ShowError("An error occurred. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Signs out the user and returns to the login page.
    /// </summary>
    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            await _authService.LogoutAsync();
            NavigateToLoginPage();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error signing out: {ex.Message}");
            // Still navigate to login even if logout fails
            NavigateToLoginPage();
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
    }

    private void NavigateToMainPage()
    {
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = new AppShell();
        }
    }

    private void NavigateToLoginPage()
    {
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = new LoginPage(
                Application.Current.Handler?.MauiContext?.Services.GetService<LoginViewModel>()
                    ?? throw new InvalidOperationException("LoginViewModel not available"));
        }
    }
}
