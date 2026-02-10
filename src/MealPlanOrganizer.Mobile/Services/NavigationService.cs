namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Shell-based navigation service implementation.
/// </summary>
public class NavigationService : INavigationService
{
    /// <inheritdoc />
    public Task GoToAsync(string route)
        => Shell.Current.GoToAsync(route);

    /// <inheritdoc />
    public Task GoToAsync(string route, IDictionary<string, object> parameters)
        => Shell.Current.GoToAsync(route, parameters);

    /// <inheritdoc />
    public Task GoBackAsync()
        => Shell.Current.GoToAsync("..");
}
