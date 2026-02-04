using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _authService;

    public LoginPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Check if user is already authenticated
        try
        {
            if (await _authService.IsAuthenticatedAsync())
            {
                await NavigateToMainPage();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking authentication state: {ex.Message}");
        }
    }

    private async void OnSignInClicked(object sender, EventArgs e)
    {
        await PerformSignIn();
    }

    private async Task PerformSignIn()
    {
        try
        {
            SetLoadingState(true);
            HideError();

            var result = await _authService.LoginAsync();

            if (result != null)
            {
                var displayName = await _authService.GetUserDisplayNameAsync();
                System.Diagnostics.Debug.WriteLine($"User signed in: {displayName}");
                await NavigateToMainPage();
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
            SetLoadingState(false);
        }
    }

    private async void OnTermsOfServiceTapped(object sender, TappedEventArgs e)
    {
        // TODO: Replace with actual Terms of Service URL
        try
        {
            await Launcher.OpenAsync(new Uri("https://example.com/terms"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open Terms of Service: {ex.Message}");
        }
    }

    private async void OnPrivacyPolicyTapped(object sender, TappedEventArgs e)
    {
        // TODO: Replace with actual Privacy Policy URL
        try
        {
            await Launcher.OpenAsync(new Uri("https://example.com/privacy"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open Privacy Policy: {ex.Message}");
        }
    }

    private async Task NavigateToMainPage()
    {
        // Navigate to main app shell
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = new AppShell();
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        SignInButton.IsEnabled = !isLoading;
        SignInButton.IsVisible = !isLoading;
        LoadingGrid.IsVisible = isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorBorder.IsVisible = true;
    }

    private void HideError()
    {
        ErrorBorder.IsVisible = false;
        ErrorLabel.Text = string.Empty;
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
