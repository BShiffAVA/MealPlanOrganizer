namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service for handling deep links from push notifications, URI schemes, and app startup.
/// Centralizes navigation logic for all deep link entry points.
/// </summary>
public interface IDeepLinkService
{
    /// <summary>
    /// Gets or sets a pending deep link action that should be processed after authentication.
    /// This is used when the app is launched via deep link but the user is not yet authenticated.
    /// </summary>
    DeepLinkAction? PendingAction { get; set; }

    /// <summary>
    /// Processes a deep link action and navigates to the appropriate page.
    /// Should be called after the user is authenticated.
    /// </summary>
    /// <param name="action">The deep link action to process.</param>
    /// <returns>True if the action was handled successfully.</returns>
    Task<bool> ProcessDeepLinkAsync(DeepLinkAction action);

    /// <summary>
    /// Processes any pending deep link action that was queued before authentication.
    /// Call this after successful login.
    /// </summary>
    /// <returns>True if a pending action was processed.</returns>
    Task<bool> ProcessPendingActionAsync();

    /// <summary>
    /// Parses a deep link URI into a DeepLinkAction.
    /// Supports custom URI scheme (mealplanorganizer://) and notification data.
    /// </summary>
    /// <param name="uri">The URI to parse.</param>
    /// <returns>The parsed action, or null if the URI is not recognized.</returns>
    DeepLinkAction? ParseUri(string uri);

    /// <summary>
    /// Parses notification data into a DeepLinkAction.
    /// </summary>
    /// <param name="data">The notification data dictionary.</param>
    /// <returns>The parsed action, or null if no action is specified.</returns>
    DeepLinkAction? ParseNotificationData(IDictionary<string, string>? data);
}

/// <summary>
/// Represents a deep link action to be processed.
/// </summary>
public class DeepLinkAction
{
    /// <summary>
    /// The type of action to perform.
    /// </summary>
    public DeepLinkActionType Type { get; init; }

    /// <summary>
    /// Additional parameters for the action.
    /// </summary>
    public Dictionary<string, string> Parameters { get; init; } = new();

    /// <summary>
    /// Creates a RateRecipe deep link action.
    /// </summary>
    public static DeepLinkAction RateRecipe(string? householdId = null)
    {
        var action = new DeepLinkAction { Type = DeepLinkActionType.RateRecipes };
        if (!string.IsNullOrEmpty(householdId))
        {
            action.Parameters["householdId"] = householdId;
        }
        return action;
    }

    /// <summary>
    /// Creates a ViewRecipe deep link action.
    /// </summary>
    public static DeepLinkAction ViewRecipe(string recipeId)
    {
        return new DeepLinkAction
        {
            Type = DeepLinkActionType.ViewRecipe,
            Parameters = new Dictionary<string, string> { ["recipeId"] = recipeId }
        };
    }

    /// <summary>
    /// Creates a ViewMealPlan deep link action.
    /// </summary>
    public static DeepLinkAction ViewMealPlan(string mealPlanId)
    {
        return new DeepLinkAction
        {
            Type = DeepLinkActionType.ViewMealPlan,
            Parameters = new Dictionary<string, string> { ["mealPlanId"] = mealPlanId }
        };
    }
}

/// <summary>
/// Types of deep link actions supported by the app.
/// </summary>
public enum DeepLinkActionType
{
    /// <summary>
    /// Navigate to QuickRateRecipePage to rate pending recipes.
    /// </summary>
    RateRecipes,

    /// <summary>
    /// Navigate to RecipeDetailPage for a specific recipe.
    /// </summary>
    ViewRecipe,

    /// <summary>
    /// Navigate to MealPlanDetailPage for a specific meal plan.
    /// </summary>
    ViewMealPlan
}
