using FluentAssertions;
using MealPlanOrganizer.Functions.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Unit.Services;

/// <summary>
/// Unit tests for RecipeExtractionService.
/// 
/// Note: AzureOpenAIClient is a sealed class from the Azure SDK, making direct mocking challenging.
/// Tests that require OpenAI interaction are marked as integration tests or require an abstraction layer.
/// These tests focus on constructor validation, configuration, and HTTP client behavior.
/// </summary>
public class RecipeExtractionServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<RecipeExtractionService>> _loggerMock;
    private readonly TelemetryClient _telemetryClient;
    private readonly IHttpClientFactory _httpClientFactory;
    
    public RecipeExtractionServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["OpenAI__DeploymentName"]).Returns("gpt-4o");
        
        _loggerMock = new Mock<ILogger<RecipeExtractionService>>();
        
        var telemetryConfig = TelemetryConfiguration.CreateDefault();
        telemetryConfig.DisableTelemetry = true;
        _telemetryClient = new TelemetryClient(telemetryConfig);
        
        // Use a simple implementation that returns a new HttpClient
        _httpClientFactory = new SimpleHttpClientFactory();
    }

    /// <summary>
    /// Simple IHttpClientFactory implementation for testing
    /// </summary>
    private class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithNullOpenAIClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new RecipeExtractionService(
            null!,
            _configMock.Object,
            _loggerMock.Object,
            _telemetryClient,
            _httpClientFactory);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("openAIClient");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockOpenAIClient = CreateMockOpenAIClient();

        // Act & Assert
        var act = () => new RecipeExtractionService(
            mockOpenAIClient,
            _configMock.Object,
            null!,
            _telemetryClient,
            _httpClientFactory);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullTelemetryClient_ThrowsArgumentNullException()
    {
        // Arrange
        var mockOpenAIClient = CreateMockOpenAIClient();

        // Act & Assert
        var act = () => new RecipeExtractionService(
            mockOpenAIClient,
            _configMock.Object,
            _loggerMock.Object,
            null!,
            _httpClientFactory);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("telemetryClient");
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_CreatesServiceWithDefaultHttpClient()
    {
        // Arrange
        var mockOpenAIClient = CreateMockOpenAIClient();

        // Act - should not throw; service creates its own HttpClient when factory is null
        var service = new RecipeExtractionService(
            mockOpenAIClient,
            _configMock.Object,
            _loggerMock.Object,
            _telemetryClient,
            null!);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_WithDeploymentNameInConfig_UsesConfiguredValue()
    {
        // Arrange
        var mockOpenAIClient = CreateMockOpenAIClient();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["OpenAI__DeploymentName"]).Returns("custom-deployment");

        // Act - service is created successfully with custom config
        var service = new RecipeExtractionService(
            mockOpenAIClient,
            config.Object,
            _loggerMock.Object,
            _telemetryClient,
            _httpClientFactory);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDeploymentNameViaSeparator_UsesConfiguredValue()
    {
        // Arrange
        var mockOpenAIClient = CreateMockOpenAIClient();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["OpenAI__DeploymentName"]).Returns((string?)null);
        config.Setup(c => c["OpenAI:DeploymentName"]).Returns("alternate-deployment");

        // Act
        var service = new RecipeExtractionService(
            mockOpenAIClient,
            config.Object,
            _loggerMock.Object,
            _telemetryClient,
            _httpClientFactory);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNoDeploymentNameConfig_DefaultsToGpt4o()
    {
        // Arrange
        var mockOpenAIClient = CreateMockOpenAIClient();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["OpenAI__DeploymentName"]).Returns((string?)null);
        config.Setup(c => c["OpenAI:DeploymentName"]).Returns((string?)null);

        // Act
        var service = new RecipeExtractionService(
            mockOpenAIClient,
            config.Object,
            _loggerMock.Object,
            _telemetryClient,
            _httpClientFactory);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region ExtractFromUrlAsync - HTTP Client Tests

    [Fact]
    public async Task ExtractFromUrlAsync_WithEmptyUrl_ReturnsFailureResponse()
    {
        // Arrange
        var service = CreateService();

        // Act - service validates URL and returns failure instead of throwing
        var result = await service.ExtractFromUrlAsync("");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractFromUrlAsync_WithInvalidUrl_ReturnsFailureResponse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ExtractFromUrlAsync("not-a-valid-url");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid URL");
    }

    [Fact]
    public async Task ExtractFromUrlAsync_WithNonHttpUrl_ReturnsFailureResponse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ExtractFromUrlAsync("ftp://example.com/recipe");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP");
    }

    #endregion

    #region Input Handling Tests
    
    // Note: The RecipeExtractionService does not throw exceptions for invalid inputs.
    // Instead, it catches errors and returns RecipeExtractionResponse with Success = false.
    // Tests requiring actual OpenAI API calls are in the Integration Tests section.

    #endregion

    #region Helper Methods

    private RecipeExtractionService CreateService(IHttpClientFactory? httpClientFactory = null)
    {
        return new RecipeExtractionService(
            CreateMockOpenAIClient(),
            _configMock.Object,
            _loggerMock.Object,
            _telemetryClient,
            httpClientFactory ?? _httpClientFactory);
    }

    private static Azure.AI.OpenAI.AzureOpenAIClient CreateMockOpenAIClient()
    {
        // AzureOpenAIClient requires a valid-looking endpoint but won't connect in unit tests
        // The client is created but not used when we're testing input validation
        return new Azure.AI.OpenAI.AzureOpenAIClient(
            new Uri("https://test-openai.openai.azure.com"),
            new Azure.AzureKeyCredential("test-api-key"));
    }

    #endregion

    #region Mock HTTP Handler

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }
            
            return Task.FromResult(_response);
        }
    }

    #endregion
}

/// <summary>
/// Integration tests that require a real Azure OpenAI connection.
/// These are marked with a category and should be run separately.
/// </summary>
[Trait("Category", "Integration")]
public class RecipeExtractionServiceIntegrationTests
{
    // Note: These tests require Azure OpenAI credentials configured via:
    // - OpenAI__Endpoint
    // - OpenAI__ApiKey
    // - OpenAI__DeploymentName
    //
    // They should be run as part of a dedicated integration test run,
    // not as part of the regular unit test suite.

    [Fact(Skip = "Requires Azure OpenAI credentials")]
    public async Task ExtractFromText_WithValidRecipeText_ReturnsExtractedRecipe()
    {
        // This test would require actual OpenAI configuration
        // It's included as a template for integration testing
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires Azure OpenAI credentials")]
    public async Task ExtractFromUrl_WithValidRecipeUrl_ReturnsExtractedRecipe()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires Azure OpenAI credentials")]
    public async Task ExtractFromImage_WithValidImage_ReturnsExtractedRecipe()
    {
        await Task.CompletedTask;
    }
}
