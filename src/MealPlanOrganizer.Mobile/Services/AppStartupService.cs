using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service for handling app startup and resume tasks, including checking for pending ratings.
/// </summary>
public class AppStartupService : IAppStartupService
{
    private readonly IRecipeService _recipeService;
    private readonly IAuthService _authService;
    private readonly ILogger<AppStartupService> _logger;
    
    // Throttle configuration - don't prompt more than once per session or within a time window
    private DateTime? _lastPromptTime;
    private static readonly TimeSpan PromptCooldown = TimeSpan.FromMinutes(30);
    
    /// <inheritdoc/>
    public bool HasPerformedStartupCheck { get; set; }

    public AppStartupService(
        IRecipeService recipeService, 
        IAuthService authService,
        ILogger<AppStartupService> logger)
    {
        _recipeService = recipeService;
        _authService = authService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> CheckPendingRatingsAsync(bool forceCheck = false)
    {
        try
        {
            // Check if user is authenticated
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                _logger.LogDebug("Skipping pending ratings check - user not authenticated");
                return false;
            }

            // Check throttle
            if (!forceCheck && !ShouldPrompt())
            {
                _logger.LogDebug("Skipping pending ratings check - within cooldown period");
                return false;
            }

            // Check if we've already done the startup check this session
            if (!forceCheck && HasPerformedStartupCheck)
            {
                _logger.LogDebug("Skipping pending ratings check - already performed this session");
                return false;
            }

            HasPerformedStartupCheck = true;

            _logger.LogInformation("Checking for pending ratings...");
            var pendingRatings = await _recipeService.GetPendingRatingsAsync();

            if (pendingRatings == null || pendingRatings.Count == 0)
            {
                _logger.LogDebug("No pending ratings found");
                return false;
            }

            _logger.LogInformation("Found {Count} pending rating(s)", pendingRatings.Count);

            // Prompt the user
            var shouldNavigate = await PromptUserAsync(pendingRatings.Count);
            
            if (shouldNavigate)
            {
                _lastPromptTime = DateTime.UtcNow;
                await NavigateToQuickRatePageAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pending ratings on app startup");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetPendingRatingsCountAsync()
    {
        try
        {
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                return 0;
            }

            var pendingRatings = await _recipeService.GetPendingRatingsAsync();
            return pendingRatings?.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending ratings count");
            return 0;
        }
    }

    /// <inheritdoc/>
    public Task OnAppResumedAsync()
    {
        // Reset the session flag when app resumes from background
        // This allows checking again after returning to the app
        HasPerformedStartupCheck = false;
        _logger.LogDebug("App resumed - reset startup check flag");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if we should prompt the user based on cooldown timing.
    /// </summary>
    private bool ShouldPrompt()
    {
        if (_lastPromptTime == null)
        {
            return true;
        }

        var elapsed = DateTime.UtcNow - _lastPromptTime.Value;
        return elapsed >= PromptCooldown;
    }

    /// <summary>
    /// Show a prompt to the user asking if they want to rate their recent meals.
    /// </summary>
    protected virtual async Task<bool> PromptUserAsync(int pendingCount)
    {
        try
        {
            var message = pendingCount == 1
                ? "You have 1 recipe waiting for your rating. Would you like to rate it now?"
                : $"You have {pendingCount} recipes waiting for your rating. Would you like to rate them now?";

            // Get the current page to display the alert
            var currentPage = GetCurrentPage();
            if (currentPage == null)
            {
                _logger.LogWarning("Cannot show prompt - no current page available");
                return false;
            }

            var result = await currentPage.DisplayAlertAsync(
                "Rate Your Meals",
                message,
                "Rate Now",
                "Later");

            _logger.LogInformation("User {Action} pending ratings prompt", result ? "accepted" : "declined");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing pending ratings prompt");
            return false;
        }
    }

    /// <summary>
    /// Navigate to the QuickRateRecipePage.
    /// </summary>
    protected virtual async Task NavigateToQuickRatePageAsync()
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(QuickRateRecipePage));
            _logger.LogInformation("Navigated to QuickRateRecipePage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to QuickRateRecipePage");
        }
    }

    /// <summary>
    /// Gets the current page for displaying prompts.
    /// </summary>
    protected virtual Page? GetCurrentPage()
    {
        try
        {
            // Try Shell.Current first
            if (Shell.Current?.CurrentPage != null)
            {
                return Shell.Current.CurrentPage;
            }

            // Fall back to the main window's page
            if (Application.Current?.Windows.Count > 0)
            {
                var window = Application.Current.Windows[0];
                if (window.Page is NavigationPage navPage)
                {
                    return navPage.CurrentPage;
                }
                return window.Page;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
