using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealPlanOrganizer.Mobile.Tests.Services;

/// <summary>
/// Unit tests for DeepLinkService functionality.
/// Tests URI parsing, notification data parsing, and action processing logic.
/// </summary>
public class DeepLinkServiceTests
{
    private readonly TestableDeepLinkService _service;
    private readonly MockNavigationServiceForDeepLink _mockNavigationService;

    public DeepLinkServiceTests()
    {
        _mockNavigationService = new MockNavigationServiceForDeepLink();
        _service = new TestableDeepLinkService(
            NullLogger<TestableDeepLinkService>.Instance,
            _mockNavigationService);
    }

    #region ParseUri Tests

    [Fact]
    public void ParseUri_RateScheme_ReturnsRateRecipesAction()
    {
        // Arrange
        var uri = "mealplanorganizer://rate";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    [Fact]
    public void ParseUri_RateRecipesScheme_ReturnsRateRecipesAction()
    {
        // Arrange
        var uri = "mealplanorganizer://rate-recipes";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    [Fact]
    public void ParseUri_RateRecipeScheme_ReturnsRateRecipesAction()
    {
        // Arrange
        var uri = "mealplanorganizer://rate-recipe";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    [Theory]
    [InlineData("mealplanorganizer://RATE")]
    [InlineData("mealplanorganizer://Rate")]
    [InlineData("MEALPLANORGANIZER://rate")]
    public void ParseUri_CaseInsensitive_ReturnsRateRecipesAction(string uri)
    {
        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    [Fact]
    public void ParseUri_RecipeWithId_ReturnsViewRecipeAction()
    {
        // Arrange
        var recipeId = Guid.NewGuid().ToString();
        var uri = $"mealplanorganizer://recipe/{recipeId}";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.ViewRecipe);
        result.Parameters.Should().ContainKey("recipeId");
        result.Parameters["recipeId"].Should().Be(recipeId);
    }

    [Fact]
    public void ParseUri_MealPlanWithId_ReturnsViewMealPlanAction()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid().ToString();
        var uri = $"mealplanorganizer://mealplan/{mealPlanId}";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.ViewMealPlan);
        result.Parameters.Should().ContainKey("mealPlanId");
        result.Parameters["mealPlanId"].Should().Be(mealPlanId);
    }

    [Fact]
    public void ParseUri_RecipeWithoutId_ReturnsNull()
    {
        // Arrange
        var uri = "mealplanorganizer://recipe";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_MealPlanWithoutId_ReturnsNull()
    {
        // Arrange
        var uri = "mealplanorganizer://mealplan";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_UnknownScheme_ReturnsNull()
    {
        // Arrange
        var uri = "otherscheme://rate";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_UnknownHost_ReturnsNull()
    {
        // Arrange
        var uri = "mealplanorganizer://unknown";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_EmptyString_ReturnsNull()
    {
        // Act
        var result = _service.ParseUri("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_NullString_ReturnsNull()
    {
        // Act
        var result = _service.ParseUri(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_WhitespaceString_ReturnsNull()
    {
        // Act
        var result = _service.ParseUri("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_InvalidUri_ReturnsNull()
    {
        // Arrange - malformed URI
        var uri = "not a valid uri at all";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseUri_TrimsWhitespace_ReturnsAction()
    {
        // Arrange
        var uri = "  mealplanorganizer://rate  ";

        // Act
        var result = _service.ParseUri(uri);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    #endregion

    #region ParseNotificationData Tests

    [Fact]
    public void ParseNotificationData_RateRecipeAction_ReturnsRateRecipesAction()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["action"] = "rate_recipe"
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    [Fact]
    public void ParseNotificationData_RateRecipesAction_ReturnsRateRecipesAction()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["action"] = "rate_recipes"
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    [Fact]
    public void ParseNotificationData_RateRecipeWithHouseholdId_IncludesHouseholdId()
    {
        // Arrange
        var householdId = Guid.NewGuid().ToString();
        var data = new Dictionary<string, string>
        {
            ["action"] = "rate_recipe",
            ["householdId"] = householdId
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
        result.Parameters.Should().ContainKey("householdId");
        result.Parameters["householdId"].Should().Be(householdId);
    }

    [Fact]
    public void ParseNotificationData_ViewRecipeAction_ReturnsViewRecipeAction()
    {
        // Arrange
        var recipeId = Guid.NewGuid().ToString();
        var data = new Dictionary<string, string>
        {
            ["action"] = "view_recipe",
            ["recipeId"] = recipeId
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.ViewRecipe);
        result.Parameters["recipeId"].Should().Be(recipeId);
    }

    [Fact]
    public void ParseNotificationData_ViewRecipeWithoutId_ReturnsNull()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["action"] = "view_recipe"
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseNotificationData_ViewMealPlanAction_ReturnsViewMealPlanAction()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid().ToString();
        var data = new Dictionary<string, string>
        {
            ["action"] = "view_mealplan",
            ["mealPlanId"] = mealPlanId
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.ViewMealPlan);
        result.Parameters["mealPlanId"].Should().Be(mealPlanId);
    }

    [Fact]
    public void ParseNotificationData_ViewMealPlanWithoutId_ReturnsNull()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["action"] = "view_mealplan"
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseNotificationData_UnknownAction_ReturnsNull()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["action"] = "unknown_action"
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseNotificationData_NoActionKey_ReturnsNull()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["recipeId"] = Guid.NewGuid().ToString()
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseNotificationData_EmptyDictionary_ReturnsNull()
    {
        // Act
        var result = _service.ParseNotificationData(new Dictionary<string, string>());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseNotificationData_NullDictionary_ReturnsNull()
    {
        // Act
        var result = _service.ParseNotificationData(null);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("RATE_RECIPE")]
    [InlineData("Rate_Recipe")]
    [InlineData("rate_RECIPE")]
    public void ParseNotificationData_CaseInsensitiveAction_ReturnsRateRecipesAction(string action)
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["action"] = action
        };

        // Act
        var result = _service.ParseNotificationData(data);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
    }

    #endregion

    #region ProcessDeepLinkAsync Tests

    [Fact]
    public async Task ProcessDeepLinkAsync_RateRecipes_NavigatesToQuickRatePage()
    {
        // Arrange
        var action = DeepLinkActionForTest.RateRecipe();

        // Act
        var result = await _service.ProcessDeepLinkAsync(action);

        // Assert
        result.Should().BeTrue();
        _mockNavigationService.LastRoute.Should().Be("QuickRateRecipePage");
    }

    [Fact]
    public async Task ProcessDeepLinkAsync_ViewRecipe_NavigatesToRecipeDetail()
    {
        // Arrange
        var recipeId = Guid.NewGuid().ToString();
        var action = DeepLinkActionForTest.ViewRecipe(recipeId);

        // Act
        var result = await _service.ProcessDeepLinkAsync(action);

        // Assert
        result.Should().BeTrue();
        _mockNavigationService.LastRoute.Should().Contain("RecipeDetailPage");
        _mockNavigationService.LastRoute.Should().Contain(recipeId);
    }

    [Fact]
    public async Task ProcessDeepLinkAsync_ViewRecipeWithoutId_ReturnsFalse()
    {
        // Arrange
        var action = new DeepLinkActionForTest { Type = DeepLinkActionTypeForTest.ViewRecipe };

        // Act
        var result = await _service.ProcessDeepLinkAsync(action);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDeepLinkAsync_ViewMealPlan_NavigatesToMealPlanDetail()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid().ToString();
        var action = DeepLinkActionForTest.ViewMealPlan(mealPlanId);

        // Act
        var result = await _service.ProcessDeepLinkAsync(action);

        // Assert
        result.Should().BeTrue();
        _mockNavigationService.LastRoute.Should().Contain("MealPlanDetailPage");
        _mockNavigationService.LastRoute.Should().Contain(mealPlanId);
    }

    [Fact]
    public async Task ProcessDeepLinkAsync_ViewMealPlanWithoutId_ReturnsFalse()
    {
        // Arrange
        var action = new DeepLinkActionForTest { Type = DeepLinkActionTypeForTest.ViewMealPlan };

        // Act
        var result = await _service.ProcessDeepLinkAsync(action);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDeepLinkAsync_NavigationThrows_ReturnsFalse()
    {
        // Arrange
        var action = DeepLinkActionForTest.RateRecipe();
        _mockNavigationService.ShouldThrow = true;

        // Act
        var result = await _service.ProcessDeepLinkAsync(action);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ProcessPendingActionAsync Tests

    [Fact]
    public async Task ProcessPendingActionAsync_NoPendingAction_ReturnsFalse()
    {
        // Act
        var result = await _service.ProcessPendingActionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPendingActionAsync_WithPendingAction_ProcessesAndClears()
    {
        // Arrange
        _service.PendingAction = DeepLinkActionForTest.RateRecipe();

        // Act
        var result = await _service.ProcessPendingActionAsync();

        // Assert
        result.Should().BeTrue();
        _service.PendingAction.Should().BeNull();
        _mockNavigationService.LastRoute.Should().Be("QuickRateRecipePage");
    }

    [Fact]
    public async Task ProcessPendingActionAsync_ClearsPendingActionBeforeProcessing()
    {
        // Arrange
        _service.PendingAction = DeepLinkActionForTest.RateRecipe();
        DeepLinkActionForTest? pendingDuringProcess = null;
        _mockNavigationService.OnNavigate = () =>
        {
            pendingDuringProcess = _service.PendingAction;
        };

        // Act
        await _service.ProcessPendingActionAsync();

        // Assert - PendingAction should be null during processing to avoid re-entry
        pendingDuringProcess.Should().BeNull();
    }

    #endregion

    #region DeepLinkAction Factory Tests

    [Fact]
    public void DeepLinkAction_RateRecipe_CreatesCorrectAction()
    {
        // Act
        var action = DeepLinkActionForTest.RateRecipe();

        // Assert
        action.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
        action.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void DeepLinkAction_RateRecipeWithHouseholdId_IncludesParameter()
    {
        // Arrange
        var householdId = "household-123";

        // Act
        var action = DeepLinkActionForTest.RateRecipe(householdId);

        // Assert
        action.Type.Should().Be(DeepLinkActionTypeForTest.RateRecipes);
        action.Parameters.Should().ContainKey("householdId");
        action.Parameters["householdId"].Should().Be(householdId);
    }

    [Fact]
    public void DeepLinkAction_ViewRecipe_CreatesCorrectAction()
    {
        // Arrange
        var recipeId = "recipe-456";

        // Act
        var action = DeepLinkActionForTest.ViewRecipe(recipeId);

        // Assert
        action.Type.Should().Be(DeepLinkActionTypeForTest.ViewRecipe);
        action.Parameters["recipeId"].Should().Be(recipeId);
    }

    [Fact]
    public void DeepLinkAction_ViewMealPlan_CreatesCorrectAction()
    {
        // Arrange
        var mealPlanId = "mealplan-789";

        // Act
        var action = DeepLinkActionForTest.ViewMealPlan(mealPlanId);

        // Assert
        action.Type.Should().Be(DeepLinkActionTypeForTest.ViewMealPlan);
        action.Parameters["mealPlanId"].Should().Be(mealPlanId);
    }

    #endregion
}

#region Test Support Classes

/// <summary>
/// Testable implementation of DeepLinkService behavior without MAUI dependencies.
/// </summary>
public class TestableDeepLinkService
{
    private readonly ILogger<TestableDeepLinkService> _logger;
    private readonly MockNavigationServiceForDeepLink _navigationService;

    public const string UriScheme = "mealplanorganizer";

    public DeepLinkActionForTest? PendingAction { get; set; }

    public TestableDeepLinkService(
        ILogger<TestableDeepLinkService> logger,
        MockNavigationServiceForDeepLink navigationService)
    {
        _logger = logger;
        _navigationService = navigationService;
    }

    public async Task<bool> ProcessDeepLinkAsync(DeepLinkActionForTest action)
    {
        try
        {
            switch (action.Type)
            {
                case DeepLinkActionTypeForTest.RateRecipes:
                    await _navigationService.GoToAsync("QuickRateRecipePage");
                    return true;

                case DeepLinkActionTypeForTest.ViewRecipe:
                    if (action.Parameters.TryGetValue("recipeId", out var recipeId))
                    {
                        await _navigationService.GoToAsync($"RecipeDetailPage?recipeId={recipeId}");
                        return true;
                    }
                    return false;

                case DeepLinkActionTypeForTest.ViewMealPlan:
                    if (action.Parameters.TryGetValue("mealPlanId", out var mealPlanId))
                    {
                        await _navigationService.GoToAsync($"MealPlanDetailPage?mealPlanId={mealPlanId}");
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> ProcessPendingActionAsync()
    {
        if (PendingAction == null)
        {
            return false;
        }

        var action = PendingAction;
        PendingAction = null; // Clear before processing

        return await ProcessDeepLinkAsync(action);
    }

    public DeepLinkActionForTest? ParseUri(string? uri)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            uri = uri.Trim();

            if (!uri.StartsWith($"{UriScheme}://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var uriObj = new Uri(uri);
            var host = uriObj.Host.ToLowerInvariant();
            var pathSegments = uriObj.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (host is "rate" or "rate-recipes" or "rate-recipe")
            {
                return DeepLinkActionForTest.RateRecipe();
            }

            if (host == "recipe" && pathSegments.Length >= 1)
            {
                return DeepLinkActionForTest.ViewRecipe(pathSegments[0]);
            }

            if (host == "mealplan" && pathSegments.Length >= 1)
            {
                return DeepLinkActionForTest.ViewMealPlan(pathSegments[0]);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public DeepLinkActionForTest? ParseNotificationData(IDictionary<string, string>? data)
    {
        if (data == null || data.Count == 0)
        {
            return null;
        }

        if (!data.TryGetValue("action", out var action))
        {
            return null;
        }

        switch (action.ToLowerInvariant())
        {
            case "rate_recipe":
            case "rate_recipes":
                data.TryGetValue("householdId", out var householdId);
                return DeepLinkActionForTest.RateRecipe(householdId);

            case "view_recipe":
                if (data.TryGetValue("recipeId", out var recipeId))
                {
                    return DeepLinkActionForTest.ViewRecipe(recipeId);
                }
                return null;

            case "view_mealplan":
                if (data.TryGetValue("mealPlanId", out var mealPlanId))
                {
                    return DeepLinkActionForTest.ViewMealPlan(mealPlanId);
                }
                return null;

            default:
                return null;
        }
    }
}

public enum DeepLinkActionTypeForTest
{
    RateRecipes,
    ViewRecipe,
    ViewMealPlan
}

public class DeepLinkActionForTest
{
    public DeepLinkActionTypeForTest Type { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();

    public static DeepLinkActionForTest RateRecipe(string? householdId = null)
    {
        var action = new DeepLinkActionForTest { Type = DeepLinkActionTypeForTest.RateRecipes };
        if (!string.IsNullOrEmpty(householdId))
        {
            action.Parameters["householdId"] = householdId;
        }
        return action;
    }

    public static DeepLinkActionForTest ViewRecipe(string recipeId)
    {
        return new DeepLinkActionForTest
        {
            Type = DeepLinkActionTypeForTest.ViewRecipe,
            Parameters = new Dictionary<string, string> { ["recipeId"] = recipeId }
        };
    }

    public static DeepLinkActionForTest ViewMealPlan(string mealPlanId)
    {
        return new DeepLinkActionForTest
        {
            Type = DeepLinkActionTypeForTest.ViewMealPlan,
            Parameters = new Dictionary<string, string> { ["mealPlanId"] = mealPlanId }
        };
    }
}

public class MockNavigationServiceForDeepLink
{
    public string? LastRoute { get; private set; }
    public bool ShouldThrow { get; set; }
    public Action? OnNavigate { get; set; }

    public Task GoToAsync(string route)
    {
        OnNavigate?.Invoke();

        if (ShouldThrow)
        {
            throw new Exception("Navigation failed");
        }

        LastRoute = route;
        return Task.CompletedTask;
    }
}

#endregion
