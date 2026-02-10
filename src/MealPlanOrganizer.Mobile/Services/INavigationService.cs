namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Abstraction for navigation to enable unit testing of ViewModels.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigate to a route.
    /// </summary>
    Task GoToAsync(string route);

    /// <summary>
    /// Navigate to a route with parameters.
    /// </summary>
    Task GoToAsync(string route, IDictionary<string, object> parameters);

    /// <summary>
    /// Navigate back to the previous page.
    /// </summary>
    Task GoBackAsync();
}
