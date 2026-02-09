using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace MealPlanOrganizer.Functions.Tests.Integration.Helpers;

/// <summary>
/// A fake HttpResponseData implementation for testing.
/// </summary>
public class FakeHttpResponseData : HttpResponseData
{
    private HttpHeadersCollection _headers;
    
    public FakeHttpResponseData(FunctionContext context) : base(context)
    {
        _headers = new HttpHeadersCollection();
        Body = new MemoryStream();
    }
    
    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers 
    { 
        get => _headers;
        set => _headers = value;
    }
    public override Stream Body { get; set; }
    public override HttpCookies Cookies => throw new NotImplementedException();
}

/// <summary>
/// Factory for creating properly configured mock HttpRequestData instances for testing.
/// Sets up FunctionContext with InstanceServices to support WriteAsJsonAsync.
/// </summary>
public static class MockHttpFactory
{
    /// <summary>
    /// Creates a mock HttpRequestData with proper FunctionContext setup for WriteAsJsonAsync support.
    /// </summary>
    public static HttpRequestData CreateRequest(
        HttpMethod method,
        string url = "http://localhost/api/test",
        string? body = null,
        string? authHeader = null)
    {
        // Create a service provider with WorkerOptions containing the JSON serializer
        var services = new ServiceCollection();
        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new Azure.Core.Serialization.JsonObjectSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
        });
        var serviceProvider = services.BuildServiceProvider();

        // Create mock FunctionContext with InstanceServices
        var context = new Mock<FunctionContext>();
        context.Setup(c => c.InstanceServices).Returns(serviceProvider);

        var request = new Mock<HttpRequestData>(context.Object);
        
        request.Setup(r => r.Method).Returns(method.ToString());
        request.Setup(r => r.Url).Returns(new Uri(url));
        
        // Set up headers
        var headers = new HttpHeadersCollection();
        if (!string.IsNullOrEmpty(authHeader))
        {
            headers.Add("Authorization", authHeader);
        }
        headers.Add("Content-Type", "application/json");
        request.Setup(r => r.Headers).Returns(headers);
        
        // Set up body
        if (body != null)
        {
            var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));
            request.Setup(r => r.Body).Returns(bodyStream);
        }
        else
        {
            request.Setup(r => r.Body).Returns(new MemoryStream());
        }
        
        // Set up response creation using FakeHttpResponseData
        request.Setup(r => r.CreateResponse()).Returns(() => new FakeHttpResponseData(context.Object));
        
        return request.Object;
    }

    /// <summary>
    /// Reads the response body as a deserialized object.
    /// </summary>
    public static async Task<T?> ReadResponseBodyAsync<T>(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var json = await reader.ReadToEndAsync();
        
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }
        
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
