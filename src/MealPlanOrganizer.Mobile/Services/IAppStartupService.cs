namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service for handling app startup and resume tasks.
/// Checks for pending ratings and prompts users when the app opens.
/// </summary>
public interface IAppStartupService
{
    /// <summary>
    /// Check for pending ratings and optionally prompt the user to rate.
    /// Should be called when the app starts or resumes.
    /// </summary>
    /// <param name="forceCheck">If true, bypasses the throttle and always checks.</param>
    /// <returns>True if pending ratings were found and user was prompted.</returns>
    Task<bool> CheckPendingRatingsAsync(bool forceCheck = false);

    /// <summary>
    /// Gets the current count of pending ratings without prompting.
    /// </summary>
    Task<int> GetPendingRatingsCountAsync();

    /// <summary>
    /// Notifies the service that the app has resumed from background.
    /// </summary>
    Task OnAppResumedAsync();

    /// <summary>
    /// Gets or sets whether the startup check has been performed this session.
    /// Reset when app goes to background.
    /// </summary>
    bool HasPerformedStartupCheck { get; set; }
}
