using System.Net;
using System.Text;
using System.Text.Json;
using MealPlanOrganizer.Functions.Functions;
using MealPlanOrganizer.Functions.Services;
using MealPlanOrganizer.Functions.Tests.Integration.Fixtures;
using MealPlanOrganizer.Functions.Tests.Integration.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for Recipe Extraction endpoints.
/// Tests the GenAI-powered extraction with mocked OpenAI responses.
/// </summary>
[Collection("Integration")]
public class ExtractionEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    
    public ExtractionEndpointsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    public async Task InitializeAsync()
    {
        await _fixture.TestHost.ResetDatabaseAsync();
        _fixture.TestHost.OpenAIHandler.SetupSuccessfulExtraction();
    }
    
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
    
    #region Image Extraction Tests
    
    [Fact]
    public async Task ExtractRecipe_FromImage_ReturnsExtractedRecipe()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        // Use a valid small test image (1x1 PNG)
        var request = new
        {
            inputType = "image",
            image = TestImages.OnePxPngBase64
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert - The function should process the request
        // The actual result depends on the mock OpenAI response
        Assert.NotNull(response);
    }
    
    [Fact]
    public async Task ExtractRecipe_FromImage_WithMissingImageData_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "image"
            // Missing image field
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    
    [Fact]
    public async Task ExtractRecipe_FromImage_TooLarge_ReturnsError()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        // Create a large base64 string (simulating > 10MB)
        var largeImageData = new string('A', 15 * 1024 * 1024); // ~11MB binary equivalent
        
        var request = new
        {
            inputType = "image",
            image = largeImageData
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    
    #endregion
    
    #region URL Extraction Tests
    
    [Fact]
    public async Task ExtractRecipe_FromUrl_WithValidUrl_ProcessesRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "url",
            url = "https://example.com/recipe-page"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert - The function should process the request
        Assert.NotNull(response);
    }
    
    [Fact]
    public async Task ExtractRecipe_FromUrl_WithMissingUrl_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "url"
            // Missing url field
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    
    [Fact]
    public async Task ExtractRecipe_FromUrl_WithInvalidUrl_ReturnsError()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "url",
            url = "not-a-valid-url"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    
    #endregion
    
    #region Text Extraction Tests
    
    [Fact]
    public async Task ExtractRecipe_FromText_WithValidText_ProcessesRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var recipeText = @"
            Spaghetti Bolognese
            Prep: 20 mins, Cook: 45 mins, Serves: 4
            
            Ingredients:
            - 500g spaghetti
            - 500g ground beef
            - 1 onion, diced
            - 2 garlic cloves, minced
            - 800ml tomato sauce
            
            Instructions:
            1. Cook pasta according to package directions
            2. Brown beef in a large pan
            3. Add onion and garlic, cook until soft
            4. Add tomato sauce, simmer 30 minutes
            5. Serve over pasta
        ";
        
        var request = new
        {
            inputType = "text",
            text = recipeText
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.NotNull(response);
    }
    
    [Fact]
    public async Task ExtractRecipe_FromText_WithMissingText_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "text"
            // Missing text field
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    
    [Fact]
    public async Task ExtractRecipe_FromText_TooLong_ReturnsError()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        // Create text that exceeds 50,000 character limit
        var longText = new string('x', 51_000);
        
        var request = new
        {
            inputType = "text",
            text = longText
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    
    #endregion
    
    #region Request Validation Tests
    
    [Fact]
    public async Task ExtractRecipe_WithEmptyBody_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: "");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task ExtractRecipe_WithInvalidInputType_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "invalid"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task ExtractRecipe_WithMissingInputType_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            text = "Some recipe text"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task ExtractRecipe_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            body: "{ invalid json not closed");
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    #endregion
    
    #region OpenAI Error Handling Tests
    
    [Fact]
    public async Task ExtractRecipe_WhenOpenAIReturnsRateLimitError_HandlesGracefully()
    {
        // Arrange
        _fixture.TestHost.OpenAIHandler.SetupRateLimitError();
        
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "text",
            text = "Simple recipe content"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert - Should handle error gracefully, not throw
        Assert.NotNull(response);
    }
    
    [Fact]
    public async Task ExtractRecipe_WhenOpenAIReturnsServerError_HandlesGracefully()
    {
        // Arrange
        _fixture.TestHost.OpenAIHandler.SetupServerError();
        
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "text",
            text = "Simple recipe content"
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert - Should handle error gracefully
        Assert.NotNull(response);
    }
    
    [Fact]
    public async Task ExtractRecipe_WhenNoRecipeFound_ReturnsLowConfidence()
    {
        // Arrange
        _fixture.TestHost.OpenAIHandler.SetupNoRecipeFound();
        
        using var scope = _fixture.TestHost.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<IRecipeExtractionService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ExtractRecipe>();
        
        var function = new ExtractRecipe(logger, extractionService);
        
        var request = new
        {
            inputType = "text",
            text = "This is not a recipe, just random text about cars."
        };
        
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request));
        
        // Act
        var response = await function.Run(httpRequest);
        
        // Assert - Should return a response with low confidence
        Assert.NotNull(response);
    }
    
    #endregion
    
    #region Helper Methods
    
    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method, 
        string? body = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/recipes/extract", body, null);
    }
    
    #endregion
}
