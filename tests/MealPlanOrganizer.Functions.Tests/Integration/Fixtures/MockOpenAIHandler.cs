using System.Net;
using System.Text.Json;

namespace MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

/// <summary>
/// Mock HTTP message handler that intercepts Azure OpenAI API calls.
/// Returns deterministic responses for recipe extraction tests.
/// </summary>
public class MockOpenAIHandler : HttpMessageHandler
{
    private readonly Queue<MockOpenAIResponse> _responseQueue = new();
    private MockOpenAIResponse _defaultResponse;
    
    public List<HttpRequestMessage> ReceivedRequests { get; } = new();
    
    public MockOpenAIHandler()
    {
        // Default successful extraction response
        _defaultResponse = new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.OK,
            ResponseBody = CreateSuccessfulExtractionResponse()
        };
    }
    
    /// <summary>
    /// Queues a specific response for the next OpenAI API call.
    /// </summary>
    public void EnqueueResponse(MockOpenAIResponse response)
    {
        _responseQueue.Enqueue(response);
    }
    
    /// <summary>
    /// Sets the default response when no queued responses are available.
    /// </summary>
    public void SetDefaultResponse(MockOpenAIResponse response)
    {
        _defaultResponse = response;
    }
    
    /// <summary>
    /// Configures to return a successful recipe extraction.
    /// </summary>
    public void SetupSuccessfulExtraction(string recipeName = "Extracted Recipe")
    {
        SetDefaultResponse(new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.OK,
            ResponseBody = CreateSuccessfulExtractionResponse(recipeName)
        });
    }
    
    /// <summary>
    /// Configures to return a low confidence extraction.
    /// </summary>
    public void SetupLowConfidenceExtraction()
    {
        SetDefaultResponse(new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.OK,
            ResponseBody = CreateLowConfidenceExtractionResponse()
        });
    }
    
    /// <summary>
    /// Configures to return a rate limit error (429).
    /// </summary>
    public void SetupRateLimitError(int retryAfterSeconds = 60)
    {
        SetDefaultResponse(new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.TooManyRequests,
            ResponseBody = JsonSerializer.Serialize(new { error = new { message = "Rate limit exceeded", type = "rate_limit_error" } }),
            Headers = new Dictionary<string, string> { { "Retry-After", retryAfterSeconds.ToString() } }
        });
    }
    
    /// <summary>
    /// Configures to return a server error (500).
    /// </summary>
    public void SetupServerError()
    {
        SetDefaultResponse(new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.InternalServerError,
            ResponseBody = JsonSerializer.Serialize(new { error = new { message = "Internal server error", type = "server_error" } })
        });
    }
    
    /// <summary>
    /// Configures to return malformed JSON.
    /// </summary>
    public void SetupMalformedResponse()
    {
        SetDefaultResponse(new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.OK,
            ResponseBody = "{ invalid json response that is not valid"
        });
    }
    
    /// <summary>
    /// Configures to return an empty/no-recipe response.
    /// </summary>
    public void SetupNoRecipeFound()
    {
        SetDefaultResponse(new MockOpenAIResponse
        {
            StatusCode = HttpStatusCode.OK,
            ResponseBody = CreateNoRecipeFoundResponse()
        });
    }
    
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        ReceivedRequests.Add(request);
        
        var mockResponse = _responseQueue.Count > 0 
            ? _responseQueue.Dequeue() 
            : _defaultResponse;
        
        var response = new HttpResponseMessage(mockResponse.StatusCode)
        {
            Content = new StringContent(mockResponse.ResponseBody, System.Text.Encoding.UTF8, "application/json"),
            RequestMessage = request
        };
        
        foreach (var header in mockResponse.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        return Task.FromResult(response);
    }
    
    private static string CreateSuccessfulExtractionResponse(string recipeName = "Extracted Spaghetti Bolognese")
    {
        // Simulate Azure OpenAI chat completion response
        var recipeJson = JsonSerializer.Serialize(new
        {
            name = recipeName,
            description = "A classic Italian pasta dish with rich meat sauce",
            cuisineType = "Italian",
            prepMinutes = 20,
            cookMinutes = 45,
            servings = 4,
            confidence = 0.95,
            ingredients = new[]
            {
                new { name = "Spaghetti", quantity = 400, unit = "g" },
                new { name = "Ground Beef", quantity = 500, unit = "g" },
                new { name = "Tomato Sauce", quantity = 800, unit = "ml" },
                new { name = "Onion", quantity = 1, unit = "large" },
                new { name = "Garlic", quantity = 3, unit = "cloves" }
            },
            steps = new[]
            {
                new { stepNumber = 1, instruction = "Cook pasta according to package directions" },
                new { stepNumber = 2, instruction = "Brown the ground beef in a large pan" },
                new { stepNumber = 3, instruction = "Add onion and garlic, cook until soft" },
                new { stepNumber = 4, instruction = "Add tomato sauce and simmer for 30 minutes" },
                new { stepNumber = 5, instruction = "Serve sauce over pasta" }
            }
        });
        
        return CreateChatCompletionResponse(recipeJson);
    }
    
    private static string CreateLowConfidenceExtractionResponse()
    {
        var recipeJson = JsonSerializer.Serialize(new
        {
            name = "Unclear Recipe",
            description = "Recipe details partially visible",
            cuisineType = (string?)null,
            prepMinutes = (int?)null,
            cookMinutes = (int?)null,
            servings = (int?)null,
            confidence = 0.45,
            ingredients = new[]
            {
                new { name = "Flour", quantity = (int?)null, unit = (string?)null },
                new { name = "Sugar", quantity = (int?)null, unit = (string?)null }
            },
            steps = new[]
            {
                new { stepNumber = 1, instruction = "Mix ingredients" }
            }
        });
        
        return CreateChatCompletionResponse(recipeJson);
    }
    
    private static string CreateNoRecipeFoundResponse()
    {
        var recipeJson = JsonSerializer.Serialize(new
        {
            name = (string?)null,
            description = "No recipe found in the provided content",
            confidence = 0.0,
            ingredients = Array.Empty<object>(),
            steps = Array.Empty<object>()
        });
        
        return CreateChatCompletionResponse(recipeJson);
    }
    
    private static string CreateChatCompletionResponse(string content)
    {
        // Azure OpenAI / OpenAI chat completion response format
        return JsonSerializer.Serialize(new
        {
            id = "chatcmpl-mock-" + Guid.NewGuid().ToString("N")[..8],
            @object = "chat.completion",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = "gpt-4o",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = content
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 500,
                completion_tokens = 200,
                total_tokens = 700
            }
        });
    }
}

/// <summary>
/// Represents a mock response for OpenAI API calls.
/// </summary>
public class MockOpenAIResponse
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
}
