using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealPlanOrganizer.Mobile.Tests.Services;

/// <summary>
/// Tests for AppStartupService that checks for pending ratings on app open.
/// Uses a testable implementation to avoid MAUI dependencies.
/// </summary>
public class AppStartupServiceTests
{
    private readonly MockRecipeServiceForAppStartup _mockRecipeService;
    private readonly MockAuthServiceForAppStartup _mockAuthService;
    private readonly TestableAppStartupService _service;

    public AppStartupServiceTests()
    {
        _mockRecipeService = new MockRecipeServiceForAppStartup();
        _mockAuthService = new MockAuthServiceForAppStartup();
        _service = new TestableAppStartupService(
            _mockRecipeService,
            _mockAuthService,
            NullLogger<TestableAppStartupService>.Instance);
    }

    #region CheckPendingRatingsAsync Tests

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenNotAuthenticated_ReturnsFalse()
    {
        _mockAuthService.IsAuthenticated = false;

        var result = await _service.CheckPendingRatingsAsync();

        result.Should().BeFalse();
        _service.PromptShown.Should().BeFalse();
        _service.NavigatedToQuickRate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenNoPendingRatings_ReturnsFalse()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>();

        var result = await _service.CheckPendingRatingsAsync();

        result.Should().BeFalse();
        _service.PromptShown.Should().BeFalse();
        _service.NavigatedToQuickRate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenPendingRatingsExist_PromptsUser()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = true;

        var result = await _service.CheckPendingRatingsAsync();

        result.Should().BeTrue();
        _service.PromptShown.Should().BeTrue();
        _service.LastPromptCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenUserAcceptsPrompt_NavigatesToQuickRate()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = true;

        await _service.CheckPendingRatingsAsync();

        _service.NavigatedToQuickRate.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenUserDeclinesPrompt_DoesNotNavigate()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = false;

        var result = await _service.CheckPendingRatingsAsync();

        result.Should().BeFalse();
        _service.NavigatedToQuickRate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenAlreadyPerformedCheck_SkipsWithoutForce()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = true;
        _service.HasPerformedStartupCheck = true;

        var result = await _service.CheckPendingRatingsAsync(forceCheck: false);

        result.Should().BeFalse();
        _service.PromptShown.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenForceCheck_BypassesStartupCheckFlag()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = true;
        _service.HasPerformedStartupCheck = true;

        var result = await _service.CheckPendingRatingsAsync(forceCheck: true);

        result.Should().BeTrue();
        _service.PromptShown.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_SetsStartupCheckFlagAfterFirstCheck()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>();
        _service.HasPerformedStartupCheck.Should().BeFalse();

        await _service.CheckPendingRatingsAsync();

        _service.HasPerformedStartupCheck.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WithMultipleRatings_ShowsCorrectCount()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Recipe 1" },
            new() { Id = Guid.NewGuid(), RecipeTitle = "Recipe 2" },
            new() { Id = Guid.NewGuid(), RecipeTitle = "Recipe 3" }
        };
        _service.PromptWillReturn = true;

        await _service.CheckPendingRatingsAsync();

        _service.LastPromptCount.Should().Be(3);
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_WhenExceptionOccurs_ReturnsFalseGracefully()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.ShouldThrow = true;

        var result = await _service.CheckPendingRatingsAsync();

        result.Should().BeFalse();
    }

    #endregion

    #region GetPendingRatingsCountAsync Tests

    [Fact]
    public async Task GetPendingRatingsCountAsync_WhenNotAuthenticated_ReturnsZero()
    {
        _mockAuthService.IsAuthenticated = false;

        var count = await _service.GetPendingRatingsCountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetPendingRatingsCountAsync_ReturnsCorrectCount()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Recipe 1" },
            new() { Id = Guid.NewGuid(), RecipeTitle = "Recipe 2" }
        };

        var count = await _service.GetPendingRatingsCountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingRatingsCountAsync_WhenNoPendingRatings_ReturnsZero()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>();

        var count = await _service.GetPendingRatingsCountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetPendingRatingsCountAsync_WhenExceptionOccurs_ReturnsZero()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.ShouldThrow = true;

        var count = await _service.GetPendingRatingsCountAsync();

        count.Should().Be(0);
    }

    #endregion

    #region OnAppResumedAsync Tests

    [Fact]
    public async Task OnAppResumedAsync_ResetsStartupCheckFlag()
    {
        _service.HasPerformedStartupCheck = true;

        await _service.OnAppResumedAsync();

        _service.HasPerformedStartupCheck.Should().BeFalse();
    }

    #endregion

    #region Cooldown/Throttle Tests

    [Fact]
    public async Task CheckPendingRatingsAsync_RespectsThrottleCooldown()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = true;

        // First check - should prompt
        await _service.CheckPendingRatingsAsync(forceCheck: true);
        _service.PromptShown.Should().BeTrue();
        
        // Reset tracking but simulate within cooldown period
        _service.ResetTracking();
        _service.HasPerformedStartupCheck = false;
        _service.SetLastPromptTimeToNow();
        
        // Second check within cooldown - should skip prompt
        var result = await _service.CheckPendingRatingsAsync(forceCheck: false);

        result.Should().BeFalse();
        _service.PromptShown.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPendingRatingsAsync_ForceCheck_BypassesCooldown()
    {
        _mockAuthService.IsAuthenticated = true;
        _mockRecipeService.PendingRatingsToReturn = new List<PendingRatingDtoForTest>
        {
            new() { Id = Guid.NewGuid(), RecipeTitle = "Test Recipe" }
        };
        _service.PromptWillReturn = true;
        _service.SetLastPromptTimeToNow();

        // Force check should bypass cooldown
        var result = await _service.CheckPendingRatingsAsync(forceCheck: true);

        result.Should().BeTrue();
        _service.PromptShown.Should().BeTrue();
    }

    #endregion
}

#region Test Support Classes

/// <summary>
/// Testable version of AppStartupService that overrides MAUI-dependent methods.
/// </summary>
public class TestableAppStartupService : IAppStartupServiceForTest
{
    private readonly IRecipeServiceForTest _recipeService;
    private readonly IAuthServiceForTest _authService;
    private readonly ILogger<TestableAppStartupService> _logger;
    
    private DateTime? _lastPromptTime;
    private static readonly TimeSpan PromptCooldown = TimeSpan.FromMinutes(30);

    public bool HasPerformedStartupCheck { get; set; }
    public bool PromptShown { get; private set; }
    public bool NavigatedToQuickRate { get; private set; }
    public int LastPromptCount { get; private set; }
    public bool PromptWillReturn { get; set; }

    public TestableAppStartupService(
        IRecipeServiceForTest recipeService,
        IAuthServiceForTest authService,
        ILogger<TestableAppStartupService> logger)
    {
        _recipeService = recipeService;
        _authService = authService;
        _logger = logger;
    }

    public async Task<bool> CheckPendingRatingsAsync(bool forceCheck = false)
    {
        try
        {
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                return false;
            }

            if (!forceCheck && !ShouldPrompt())
            {
                return false;
            }

            if (!forceCheck && HasPerformedStartupCheck)
            {
                return false;
            }

            HasPerformedStartupCheck = true;

            var pendingRatings = await _recipeService.GetPendingRatingsAsync();

            if (pendingRatings == null || pendingRatings.Count == 0)
            {
                return false;
            }

            var shouldNavigate = await PromptUserAsync(pendingRatings.Count);

            if (shouldNavigate)
            {
                _lastPromptTime = DateTime.UtcNow;
                await NavigateToQuickRatePageAsync();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

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
        catch
        {
            return 0;
        }
    }

    public Task OnAppResumedAsync()
    {
        HasPerformedStartupCheck = false;
        return Task.CompletedTask;
    }

    private bool ShouldPrompt()
    {
        if (_lastPromptTime == null)
        {
            return true;
        }

        var elapsed = DateTime.UtcNow - _lastPromptTime.Value;
        return elapsed >= PromptCooldown;
    }

    private Task<bool> PromptUserAsync(int pendingCount)
    {
        PromptShown = true;
        LastPromptCount = pendingCount;
        return Task.FromResult(PromptWillReturn);
    }

    private Task NavigateToQuickRatePageAsync()
    {
        NavigatedToQuickRate = true;
        return Task.CompletedTask;
    }

    // Test helpers
    public void ResetTracking()
    {
        PromptShown = false;
        NavigatedToQuickRate = false;
        LastPromptCount = 0;
    }

    public void SetLastPromptTimeToNow()
    {
        _lastPromptTime = DateTime.UtcNow;
    }
}

public interface IAppStartupServiceForTest
{
    Task<bool> CheckPendingRatingsAsync(bool forceCheck = false);
    Task<int> GetPendingRatingsCountAsync();
    Task OnAppResumedAsync();
    bool HasPerformedStartupCheck { get; set; }
}

public interface IAuthServiceForTest
{
    Task<bool> IsAuthenticatedAsync();
}

public interface IRecipeServiceForTest
{
    Task<List<PendingRatingDtoForTest>> GetPendingRatingsAsync();
}

public class PendingRatingDtoForTest
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeTitle { get; set; } = string.Empty;
    public string? RecipeImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid MealPlanRecipeId { get; set; }
    public DateTime ServedDate { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class MockAuthServiceForAppStartup : IAuthServiceForTest
{
    public bool IsAuthenticated { get; set; }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(IsAuthenticated);
    }
}

public class MockRecipeServiceForAppStartup : IRecipeServiceForTest
{
    public List<PendingRatingDtoForTest> PendingRatingsToReturn { get; set; } = new();
    public bool ShouldThrow { get; set; }

    public Task<List<PendingRatingDtoForTest>> GetPendingRatingsAsync()
    {
        if (ShouldThrow)
        {
            throw new Exception("Test exception");
        }
        return Task.FromResult(PendingRatingsToReturn);
    }
}

#endregion
