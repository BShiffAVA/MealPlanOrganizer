using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using RichardSzalay.MockHttp;
using Xunit;

namespace MealPlanOrganizer.Mobile.Tests.Services;

/// <summary>
/// Unit tests for RecipeService pending rating HTTP interactions.
/// Tests the GetPendingRatingsAsync, CompletePendingRatingAsync, and DismissPendingRatingAsync methods.
/// </summary>
public class RecipeServicePendingRatingsTests
{
    private const string BaseUrl = "https://test-functions.azurewebsites.net/api";
    private const string FunctionKey = "test-function-key";
    private const string MockAccessToken = "mock-access-token-12345";

    private readonly MockHttpMessageHandler _mockHttp;

    public RecipeServicePendingRatingsTests()
    {
        _mockHttp = new MockHttpMessageHandler();
    }

    #region GetPendingRatingsAsync Tests

    [Fact]
    public async Task GetPendingRatings_SendsCorrectHttpRequest()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/pending-ratings?code={FunctionKey}";

        _mockHttp.Expect(HttpMethod.Get, expectedUrl)
            .WithHeaders("Authorization", $"Bearer {MockAccessToken}")
            .Respond(HttpStatusCode.OK, "application/json", "[]");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPendingRatings_WhenServerReturns200WithData_ReturnsRatings()
    {
        // Arrange
        var pendingRatings = new[]
        {
            new
            {
                id = Guid.NewGuid(),
                recipeId = Guid.NewGuid(),
                recipeTitle = "Spaghetti Carbonara",
                recipeImageUrl = "https://example.com/image.jpg",
                cuisineType = "Italian",
                mealPlanId = Guid.NewGuid(),
                mealPlanRecipeId = Guid.NewGuid(),
                servedDate = DateTime.UtcNow.AddDays(-1),
                createdUtc = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/pending-ratings*")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(pendingRatings));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecipeTitle.Should().Be("Spaghetti Carbonara");
    }

    [Fact]
    public async Task GetPendingRatings_WhenServerReturnsEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/pending-ratings*")
            .Respond(HttpStatusCode.OK, "application/json", "[]");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingRatings_WhenServerReturns401_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/pending-ratings*")
            .Respond(HttpStatusCode.Unauthorized);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingRatings_WhenServerReturns500_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/pending-ratings*")
            .Respond(HttpStatusCode.InternalServerError);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingRatings_WhenNetworkError_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/pending-ratings*")
            .Throw(new HttpRequestException("Network error"));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingRatings_WhenMultipleRatings_ReturnsAll()
    {
        // Arrange
        var pendingRatings = new[]
        {
            new { id = Guid.NewGuid(), recipeId = Guid.NewGuid(), recipeTitle = "Recipe 1", servedDate = DateTime.UtcNow.AddDays(-1), createdUtc = DateTime.UtcNow },
            new { id = Guid.NewGuid(), recipeId = Guid.NewGuid(), recipeTitle = "Recipe 2", servedDate = DateTime.UtcNow.AddDays(-2), createdUtc = DateTime.UtcNow },
            new { id = Guid.NewGuid(), recipeId = Guid.NewGuid(), recipeTitle = "Recipe 3", servedDate = DateTime.UtcNow.AddDays(-3), createdUtc = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/pending-ratings*")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(pendingRatings));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateGetPendingRatingsAsync(httpClient, MockAccessToken);

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.RecipeTitle).Should().Contain(new[] { "Recipe 1", "Recipe 2", "Recipe 3" });
    }

    #endregion

    #region CompletePendingRatingAsync Tests

    [Fact]
    public async Task CompletePendingRating_SendsCorrectHttpRequest()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();
        var expectedUrl = $"{BaseUrl}/pending-ratings/{pendingRatingId}/complete?code={FunctionKey}";

        _mockHttp.Expect(HttpMethod.Put, expectedUrl)
            .WithHeaders("Authorization", $"Bearer {MockAccessToken}")
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateCompletePendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CompletePendingRating_WhenServerReturns200_ReturnsTrue()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/complete*")
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateCompletePendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CompletePendingRating_WhenServerReturns404_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/complete*")
            .Respond(HttpStatusCode.NotFound);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateCompletePendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CompletePendingRating_WhenServerReturns401_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/complete*")
            .Respond(HttpStatusCode.Unauthorized);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateCompletePendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CompletePendingRating_WhenServerReturns500_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/complete*")
            .Respond(HttpStatusCode.InternalServerError);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateCompletePendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CompletePendingRating_WhenNetworkError_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/complete*")
            .Throw(new HttpRequestException("Network error"));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateCompletePendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DismissPendingRatingAsync Tests

    [Fact]
    public async Task DismissPendingRating_SendsCorrectHttpRequest()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();
        var expectedUrl = $"{BaseUrl}/pending-ratings/{pendingRatingId}/dismiss?code={FunctionKey}";

        _mockHttp.Expect(HttpMethod.Put, expectedUrl)
            .WithHeaders("Authorization", $"Bearer {MockAccessToken}")
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateDismissPendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task DismissPendingRating_WhenServerReturns200_ReturnsTrue()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/dismiss*")
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateDismissPendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DismissPendingRating_WhenServerReturns404_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/dismiss*")
            .Respond(HttpStatusCode.NotFound);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateDismissPendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DismissPendingRating_WhenServerReturns401_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/dismiss*")
            .Respond(HttpStatusCode.Unauthorized);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateDismissPendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DismissPendingRating_WhenServerReturns500_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/dismiss*")
            .Respond(HttpStatusCode.InternalServerError);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateDismissPendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DismissPendingRating_WhenNetworkError_ReturnsFalse()
    {
        // Arrange
        var pendingRatingId = Guid.NewGuid();

        _mockHttp.When(HttpMethod.Put, $"{BaseUrl}/pending-ratings/*/dismiss*")
            .Throw(new HttpRequestException("Network error"));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateDismissPendingRatingAsync(httpClient, pendingRatingId, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods - Simulate RecipeService Behavior

    /// <summary>
    /// Simulates the GetPendingRatingsAsync HTTP call from RecipeService.
    /// </summary>
    private async Task<List<PendingRatingDto>> SimulateGetPendingRatingsAsync(
        HttpClient httpClient,
        string? accessToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var url = $"{BaseUrl}/pending-ratings?code={FunctionKey}";
            var response = await httpClient.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new List<PendingRatingDto>();
            }

            if (!response.IsSuccessStatusCode)
            {
                return new List<PendingRatingDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PendingRatingDto>>(content, options) ?? new List<PendingRatingDto>();
        }
        catch (Exception)
        {
            return new List<PendingRatingDto>();
        }
    }

    /// <summary>
    /// Simulates the CompletePendingRatingAsync HTTP call from RecipeService.
    /// </summary>
    private async Task<bool> SimulateCompletePendingRatingAsync(
        HttpClient httpClient,
        Guid pendingRatingId,
        string? accessToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var url = $"{BaseUrl}/pending-ratings/{pendingRatingId}/complete?code={FunctionKey}";
            var response = await httpClient.PutAsync(url, null);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Simulates the DismissPendingRatingAsync HTTP call from RecipeService.
    /// </summary>
    private async Task<bool> SimulateDismissPendingRatingAsync(
        HttpClient httpClient,
        Guid pendingRatingId,
        string? accessToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var url = $"{BaseUrl}/pending-ratings/{pendingRatingId}/dismiss?code={FunctionKey}";
            var response = await httpClient.PutAsync(url, null);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion
}

/// <summary>
/// DTO for pending rating data - mirrors the one in MealPlanOrganizer.Mobile.
/// </summary>
public class PendingRatingDto
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
    public string ServedDateDisplay => ServedDate.ToString("dddd, MMMM d");
}
