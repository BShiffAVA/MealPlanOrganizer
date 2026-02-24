using Microsoft.Extensions.Logging;
using Serilog;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Implementation of IDeepLinkService for handling deep links from push notifications,
/// URI schemes, and app startup.
/// </summary>
public class DeepLinkService : IDeepLinkService
{
    private readonly ILogger<DeepLinkService> _logger;
    private readonly INavigationService _navigationService;

    /// <summary>
    /// Custom URI scheme for the app.
    /// Supports: mealplanorganizer://rate, mealplanorganizer://recipe/{id}, mealplanorganizer://mealplan/{id}
    /// </summary>
    public const string UriScheme = "mealplanorganizer";

    /// <summary>
    /// Static flag set immediately when a deep link notification is received.
    /// This allows checking for deep link processing before the service instance is available.
    /// </summary>
    public static bool HasPendingDeepLinkNotification { get; set; }

    /// <inheritdoc/>
    public DeepLinkAction? PendingAction { get; set; }

    /// <inheritdoc/>
    public bool IsProcessingDeepLink { get; private set; }

    public DeepLinkService(ILogger<DeepLinkService> logger, INavigationService navigationService)
    {
        _logger = logger;
        _navigationService = navigationService;
    }

    /// <inheritdoc/>
    public async Task<bool> ProcessDeepLinkAsync(DeepLinkAction action)
    {
        try
        {
            IsProcessingDeepLink = true;
            _logger.LogInformation("Processing deep link action: {ActionType}", action.Type);

            switch (action.Type)
            {
                case DeepLinkActionType.RateRecipes:
                    await NavigateToRateRecipesAsync();
                    return true;

                case DeepLinkActionType.ViewRecipe:
                    if (action.Parameters.TryGetValue("recipeId", out var recipeId))
                    {
                        await NavigateToRecipeAsync(recipeId);
                        return true;
                    }
                    _logger.LogWarning("ViewRecipe action missing recipeId parameter");
                    return false;

                case DeepLinkActionType.ViewMealPlan:
                    if (action.Parameters.TryGetValue("mealPlanId", out var mealPlanId))
                    {
                        await NavigateToMealPlanAsync(mealPlanId);
                        return true;
                    }
                    _logger.LogWarning("ViewMealPlan action missing mealPlanId parameter");
                    return false;

                default:
                    _logger.LogWarning("Unknown deep link action type: {ActionType}", action.Type);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing deep link action: {ActionType}", action.Type);
            return false;
        }
        finally
        {
            // Clear flags after processing completes (with a small delay to allow navigation to settle)
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                IsProcessingDeepLink = false;
                HasPendingDeepLinkNotification = false;
            });
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ProcessPendingActionAsync()
    {
        if (PendingAction == null)
        {
            _logger.LogDebug("No pending deep link action to process");
            return false;
        }

        var action = PendingAction;
        PendingAction = null; // Clear before processing to avoid re-entry

        _logger.LogInformation("Processing pending deep link action: {ActionType}", action.Type);
        return await ProcessDeepLinkAsync(action);
    }

    /// <inheritdoc/>
    public DeepLinkAction? ParseUri(string uri)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            // Normalize the URI
            uri = uri.Trim();

            // Check for our custom scheme
            if (!uri.StartsWith($"{UriScheme}://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("URI does not match scheme {Scheme}: {Uri}", UriScheme, uri);
                return null;
            }

            // Parse the URI
            var uriObj = new Uri(uri);
            var host = uriObj.Host.ToLowerInvariant();
            var pathSegments = uriObj.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            _logger.LogDebug("Parsing deep link URI - Host: {Host}, Path segments: {Segments}", 
                host, string.Join("/", pathSegments));

            // mealplanorganizer://rate or mealplanorganizer://rate-recipes
            if (host is "rate" or "rate-recipes" or "rate-recipe")
            {
                return DeepLinkAction.RateRecipe();
            }

            // mealplanorganizer://recipe/{id}
            if (host == "recipe" && pathSegments.Length >= 1)
            {
                return DeepLinkAction.ViewRecipe(pathSegments[0]);
            }

            // mealplanorganizer://mealplan/{id}
            if (host == "mealplan" && pathSegments.Length >= 1)
            {
                return DeepLinkAction.ViewMealPlan(pathSegments[0]);
            }

            _logger.LogWarning("Unrecognized deep link URI: {Uri}", uri);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing deep link URI: {Uri}", uri);
            return null;
        }
    }

    /// <inheritdoc/>
    public DeepLinkAction? ParseNotificationData(IDictionary<string, string>? data)
    {
        if (data == null || data.Count == 0)
        {
            return null;
        }

        // Check for action field
        if (!data.TryGetValue("action", out var action))
        {
            return null;
        }

        _logger.LogDebug("Parsing notification data with action: {Action}", action);

        switch (action.ToLowerInvariant())
        {
            case "rate_recipe":
            case "rate_recipes":
                data.TryGetValue("householdId", out var householdId);
                return DeepLinkAction.RateRecipe(householdId);

            case "view_recipe":
                if (data.TryGetValue("recipeId", out var recipeId))
                {
                    return DeepLinkAction.ViewRecipe(recipeId);
                }
                _logger.LogWarning("view_recipe action missing recipeId");
                return null;

            case "view_mealplan":
                if (data.TryGetValue("mealPlanId", out var mealPlanId))
                {
                    return DeepLinkAction.ViewMealPlan(mealPlanId);
                }
                _logger.LogWarning("view_mealplan action missing mealPlanId");
                return null;

            default:
                _logger.LogWarning("Unrecognized notification action: {Action}", action);
                return null;
        }
    }

    private async Task NavigateToRateRecipesAsync()
    {
        _logger.LogInformation("Navigating to QuickRateRecipePage from deep link");

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(QuickRateRecipePage));
                _logger.LogInformation("Successfully navigated to QuickRateRecipePage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to QuickRateRecipePage");
            }
        });
    }

    private async Task NavigateToRecipeAsync(string recipeId)
    {
        _logger.LogInformation("Navigating to RecipeDetailPage for recipe {RecipeId} from deep link", recipeId);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync($"{nameof(RecipeDetailPage)}?recipeId={recipeId}");
                _logger.LogInformation("Successfully navigated to RecipeDetailPage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to RecipeDetailPage");
            }
        });
    }

    private async Task NavigateToMealPlanAsync(string mealPlanId)
    {
        _logger.LogInformation("Navigating to MealPlanDetailPage for meal plan {MealPlanId} from deep link", mealPlanId);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync($"{nameof(MealPlanDetailPage)}?mealPlanId={mealPlanId}");
                _logger.LogInformation("Successfully navigated to MealPlanDetailPage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to MealPlanDetailPage");
            }
        });
    }
}
