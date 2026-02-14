using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace MealPlanOrganizer.Mobile.Tests.Services;

/// <summary>
/// Unit tests for push notification service HTTP interactions.
/// Tests the device registration and unregistration HTTP calls without MAUI runtime dependencies.
/// </summary>
public class PushNotificationServiceHttpTests
{
    private const string BaseUrl = "https://test-functions.azurewebsites.net/api";
    private const string FunctionKey = "test-function-key";
    private const string MockAccessToken = "mock-access-token-12345";
    private const string MockPushToken = "mock-push-token-67890";

    private readonly MockHttpMessageHandler _mockHttp;

    public PushNotificationServiceHttpTests()
    {
        _mockHttp = new MockHttpMessageHandler();
    }

    #region RegisterDevice HTTP Tests

    [Fact]
    public async Task RegisterDevice_SendsCorrectHttpRequest()
    {
        // Arrange
        var expectedPayload = new { platform = "windows", pushToken = MockPushToken };
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.Expect(HttpMethod.Post, expectedUrl)
            .WithContent(JsonSerializer.Serialize(expectedPayload))
            .WithHeaders("Authorization", $"Bearer {MockAccessToken}")
            .Respond(HttpStatusCode.OK, "application/json", """{"success": true}""");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateRegisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("ios")]
    [InlineData("android")]
    [InlineData("windows")]
    public async Task RegisterDevice_SendsCorrectPlatform(string platform)
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.Expect(HttpMethod.Post, expectedUrl)
            .With(req =>
            {
                var content = req.Content!.ReadAsStringAsync().Result;
                var payload = JsonSerializer.Deserialize<JsonElement>(content);
                return payload.GetProperty("platform").GetString() == platform;
            })
            .Respond(HttpStatusCode.OK, "application/json", """{"success": true}""");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateRegisterDeviceAsync(httpClient, platform, MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RegisterDevice_WithBearerToken_AttachesAuthHeader()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.Expect(HttpMethod.Post, expectedUrl)
            .With(req =>
            {
                var authHeader = req.Headers.Authorization;
                return authHeader != null &&
                       authHeader.Scheme == "Bearer" &&
                       authHeader.Parameter == MockAccessToken;
            })
            .Respond(HttpStatusCode.OK, "application/json", """{"success": true}""");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateRegisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RegisterDevice_WhenServerReturns200_ReturnsTrue()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.When(HttpMethod.Post, expectedUrl)
            .Respond(HttpStatusCode.OK, "application/json", """{"success": true}""");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateRegisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterDevice_WhenServerReturns400_ReturnsFalse()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.When(HttpMethod.Post, expectedUrl)
            .Respond(HttpStatusCode.BadRequest, "application/json", """{"error": "Invalid platform"}""");

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateRegisterDeviceAsync(httpClient, "invalid", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterDevice_WhenServerReturns401_ReturnsFalse()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.When(HttpMethod.Post, expectedUrl)
            .Respond(HttpStatusCode.Unauthorized);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateRegisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterDevice_WhenServerReturns500_ReturnsFalse()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.When(HttpMethod.Post, expectedUrl)
            .Respond(HttpStatusCode.InternalServerError);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateRegisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterDevice_WhenNetworkError_ReturnsFalse()
    {
        // Arrange
        var expectedUrl = $"{BaseUrl}/devices/register?code={FunctionKey}";

        _mockHttp.When(HttpMethod.Post, expectedUrl)
            .Throw(new HttpRequestException("Network error"));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateRegisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UnregisterDevice HTTP Tests

    [Fact]
    public async Task UnregisterDevice_SendsCorrectHttpRequest()
    {
        // Arrange
        var encodedToken = Uri.EscapeDataString(MockPushToken);
        var expectedUrl = $"{BaseUrl}/devices/unregister?code={FunctionKey}&platform=windows&pushToken={encodedToken}";

        _mockHttp.Expect(HttpMethod.Delete, expectedUrl)
            .WithHeaders("Authorization", $"Bearer {MockAccessToken}")
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateUnregisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task UnregisterDevice_EncodesTokenInUrl()
    {
        // Arrange
        var tokenWithSpecialChars = "token+with/special=chars";
        var encodedToken = Uri.EscapeDataString(tokenWithSpecialChars);
        var expectedUrl = $"{BaseUrl}/devices/unregister?code={FunctionKey}&platform=ios&pushToken={encodedToken}";

        _mockHttp.Expect(HttpMethod.Delete, expectedUrl)
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        await SimulateUnregisterDeviceAsync(httpClient, "ios", tokenWithSpecialChars, MockAccessToken);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task UnregisterDevice_WhenServerReturns200_ReturnsTrue()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, $"{BaseUrl}/devices/unregister*")
            .Respond(HttpStatusCode.OK);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateUnregisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterDevice_WhenServerReturns404_ReturnsFalse()
    {
        // Arrange - 404 means device not found, which could happen if already unregistered
        _mockHttp.When(HttpMethod.Delete, $"{BaseUrl}/devices/unregister*")
            .Respond(HttpStatusCode.NotFound);

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateUnregisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterDevice_WhenNetworkError_ReturnsFalse()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, $"{BaseUrl}/devices/unregister*")
            .Throw(new HttpRequestException("Connection refused"));

        var httpClient = _mockHttp.ToHttpClient();

        // Act
        var result = await SimulateUnregisterDeviceAsync(httpClient, "windows", MockPushToken, MockAccessToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods - Simulate PushNotificationService Behavior

    /// <summary>
    /// Simulates the RegisterDeviceAsync HTTP call from PushNotificationService.
    /// This isolates the HTTP behavior for testing without MAUI dependencies.
    /// </summary>
    private async Task<bool> SimulateRegisterDeviceAsync(
        HttpClient httpClient,
        string platform,
        string pushToken,
        string? accessToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var requestBody = JsonSerializer.Serialize(new
            {
                platform = platform,
                pushToken = pushToken
            });

            var url = $"{BaseUrl}/devices/register?code={FunctionKey}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Simulates the UnregisterDeviceAsync HTTP call from PushNotificationService.
    /// </summary>
    private async Task<bool> SimulateUnregisterDeviceAsync(
        HttpClient httpClient,
        string platform,
        string pushToken,
        string? accessToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var url = $"{BaseUrl}/devices/unregister?code={FunctionKey}&platform={platform}&pushToken={Uri.EscapeDataString(pushToken)}";
            var response = await httpClient.DeleteAsync(url);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion
}
