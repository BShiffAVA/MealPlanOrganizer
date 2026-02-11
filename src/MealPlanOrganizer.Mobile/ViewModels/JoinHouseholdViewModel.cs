using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the JoinHouseholdPage.
/// Handles joining a household with an invite code.
/// </summary>
public partial class JoinHouseholdViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _inviteCode = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isCodeValid;

    [ObservableProperty]
    private string _householdName = string.Empty;

    [ObservableProperty]
    private string _userDisplayName = string.Empty;

    public JoinHouseholdViewModel(IUserService userService, IAuthService authService)
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
    /// Validates the entered invite code.
    /// </summary>
    [RelayCommand]
    private async Task ValidateCodeAsync()
    {
        var code = InviteCode?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            IsCodeValid = false;
            HouseholdName = string.Empty;
            IsErrorVisible = false;
            return;
        }

        if (code.Length != 8)
        {
            IsCodeValid = false;
            HouseholdName = string.Empty;
            ShowError("Invite codes are 8 characters.");
            return;
        }

        try
        {
            IsValidating = true;
            IsErrorVisible = false;

            var result = await _userService.ValidateInviteCodeAsync(code);

            if (result?.IsValid == true)
            {
                IsCodeValid = true;
                HouseholdName = result.HouseholdName ?? string.Empty;
                IsErrorVisible = false;
            }
            else
            {
                IsCodeValid = false;
                HouseholdName = string.Empty;
                ShowError(result?.ErrorMessage ?? "Invalid invite code.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error validating code: {ex.Message}");
            IsCodeValid = false;
            ShowError("Unable to validate code. Please try again.");
        }
        finally
        {
            IsValidating = false;
        }
    }

    /// <summary>
    /// Joins the household using the validated invite code.
    /// </summary>
    [RelayCommand]
    private async Task JoinHouseholdAsync()
    {
        var code = InviteCode?.Trim().ToUpperInvariant() ?? string.Empty;

        if (!IsCodeValid)
        {
            ShowError("Please enter a valid invite code first.");
            return;
        }

        try
        {
            IsLoading = true;
            IsErrorVisible = false;

            var result = await _userService.JoinHouseholdAsync(code);

            if (result?.Success == true)
            {
                System.Diagnostics.Debug.WriteLine($"Joined household: {result.HouseholdName} ({result.HouseholdId})");
                NavigateToMainPage();
            }
            else
            {
                ShowError("Failed to join household. The code may have been used or expired.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error joining household: {ex.Message}");
            ShowError("An error occurred. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Navigates back to the create/join selection page.
    /// </summary>
    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
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

    partial void OnInviteCodeChanged(string value)
    {
        // Auto-validate when code reaches 8 characters
        if (value?.Trim().Length == 8)
        {
            _ = ValidateCodeAsync();
        }
        else
        {
            IsCodeValid = false;
            HouseholdName = string.Empty;
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
