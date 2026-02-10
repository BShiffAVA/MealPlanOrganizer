using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the LoginPage.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Checks if the user is already authenticated and navigates to main if so.
    /// </summary>
    [RelayCommand]
    private async Task CheckAuthenticationAsync()
    {
        try
        {
            if (await _authService.IsAuthenticatedAsync())
            {
                NavigateToMainPage();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking authentication state: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs the sign-in flow using Microsoft Entra External ID.
    /// </summary>
    [RelayCommand]
    private async Task SignInAsync()
    {
        try
        {
            IsLoading = true;
            IsErrorVisible = false;

            var result = await _authService.LoginAsync();

            if (result != null)
            {
                var displayName = await _authService.GetUserDisplayNameAsync();
                System.Diagnostics.Debug.WriteLine($"User signed in: {displayName}");
                NavigateToMainPage();
            }
            else
            {
                ShowError("Sign in was cancelled or failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sign in error: {ex.Message}");
            ShowError(GetUserFriendlyErrorMessage(ex));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens the Terms of Service URL in the browser.
    /// </summary>
    [RelayCommand]
    private async Task OpenTermsOfServiceAsync()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://example.com/terms"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open Terms of Service: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the Privacy Policy URL in the browser.
    /// </summary>
    [RelayCommand]
    private async Task OpenPrivacyPolicyAsync()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://example.com/privacy"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open Privacy Policy: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
    }

    private void NavigateToMainPage()
    {
        // Navigate to main app shell
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = new AppShell();
        }
    }

    private static string GetUserFriendlyErrorMessage(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();

        if (message.Contains("cancel"))
        {
            return "Sign in was cancelled.";
        }
        if (message.Contains("network") || message.Contains("connection"))
        {
            return "Network error. Please check your internet connection and try again.";
        }
        if (message.Contains("invalid") || message.Contains("unauthorized"))
        {
            return "Invalid credentials. Please try again.";
        }
        if (message.Contains("timeout"))
        {
            return "The request timed out. Please try again.";
        }

        return "An error occurred during sign in. Please try again.";
    }
}
