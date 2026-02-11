using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the LoginPage.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private string _loadingMessage = "Signing in...";
    
    // CancellationTokenSource to cancel ongoing sign-in operations
    private CancellationTokenSource? _signInCts;

    public LoginViewModel(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }
    
    /// <summary>
    /// Cancels the current sign-in operation and resets UI state.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _signInCts?.Cancel();
        _signInCts = null;
        IsLoading = false;
        LoadingMessage = "Signing in...";
        System.Diagnostics.Debug.WriteLine("Sign-in cancelled by user");
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
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SignInAsync()
    {
        // Cancel any previous sign-in operation
        _signInCts?.Cancel();
        _signInCts = new CancellationTokenSource();
        var cancellationToken = _signInCts.Token;
        
        try
        {
            IsLoading = true;
            IsErrorVisible = false;
            LoadingMessage = "Signing in...";

            var result = await _authService.LoginAsync();
            
            // Check if cancelled while waiting for browser
            if (cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine("Sign-in result ignored - operation was cancelled");
                return;
            }

            if (result != null)
            {
                var displayName = await _authService.GetUserDisplayNameAsync();
                System.Diagnostics.Debug.WriteLine($"User signed in: {displayName}");
                
                // Register user in backend database
                LoadingMessage = "Setting up your account...";
                var user = await _userService.RegisterUserAsync();
                
                if (user == null)
                {
                    ShowError("Failed to register your account. Please try again.");
                    return;
                }
                
                // Check if user has a household
                if (user.Household == null)
                {
                    // Navigate to household creation
                    await NavigateToCreateHouseholdAsync();
                }
                else
                {
                    // Navigate to main app
                    NavigateToMainPage();
                }
            }
            else
            {
                ShowError("Sign in was cancelled or failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"Sign in error: {ex.Message}");
                ShowError(GetUserFriendlyErrorMessage(ex));
            }
        }
        finally
        {
            // Only reset state if this operation wasn't cancelled
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
                LoadingMessage = "Signing in...";
            }
        }
    }
    
    /// <summary>
    /// Performs the sign-up flow (same as sign-in, Entra handles both).
    /// </summary>
    [RelayCommand]
    private async Task SignUpAsync()
    {
        // Entra External ID handles sign-up through the same flow
        await SignInAsync();
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
    
    private Task NavigateToCreateHouseholdAsync()
    {
        // Navigate to household creation page
        // Since we're outside the Shell (on LoginPage), we set the Window's Page directly
        if (Application.Current?.Windows.Count > 0)
        {
            var services = Application.Current.Handler?.MauiContext?.Services;
            if (services != null)
            {
                var page = services.GetRequiredService<CreateHouseholdPage>();
                Application.Current.Windows[0].Page = page;
            }
        }
        return Task.CompletedTask;
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
