using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

/// <summary>
/// Test host factory for Azure Functions integration tests.
/// Provides configured services with test dependencies (SQLite in-memory, in-memory blob storage).
/// No Docker required.
/// </summary>
public class FunctionsTestHost : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InMemoryBlobFixture _blobFixture;
    private readonly MockOpenAIHandler _mockOpenAIHandler;
    private ServiceProvider? _serviceProvider;
    
    public MockOpenAIHandler OpenAIHandler => _mockOpenAIHandler;
    
    public FunctionsTestHost(DatabaseFixture databaseFixture, InMemoryBlobFixture blobFixture)
    {
        _databaseFixture = databaseFixture;
        _blobFixture = blobFixture;
        _mockOpenAIHandler = new MockOpenAIHandler();
    }
    
    public async Task InitializeAsync()
    {
        // Build service provider with test configuration
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        // Add test configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sql"] = _databaseFixture.ConnectionString,
                ["BlobStorage:ContainerName"] = InMemoryBlobFixture.ContainerName,
                ["OpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["OpenAI:ApiKey"] = "test-api-key",
                ["OpenAI:DeploymentName"] = "gpt-4o",
                ["Jwt:Issuer"] = TestAuthHandler.TestIssuer,
                ["Jwt:Audience"] = TestAuthHandler.TestAudience,
                // Azure AD settings for JwtValidationService (test values)
                ["AzureAd:TenantId"] = "test-tenant-id",
                ["AzureAd:ClientId"] = "test-client-id",
                ["AzureAd:Authority"] = "https://test.ciamlogin.com/test-tenant-id/v2.0"
            })
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        
        // Add DbContext with SQLite (shared connection for in-memory mode)
        services.AddScoped<AppDbContext>(sp =>
        {
            return _databaseFixture.CreateDbContext();
        });
        
        // Add in-memory blob service
        services.AddSingleton<IBlobUrlService>(_blobFixture.BlobService);
        services.AddSingleton(_blobFixture.BlobService);
        
        // Add HTTP client factory with mock OpenAI handler
        services.AddHttpClient("OpenAI").ConfigurePrimaryHttpMessageHandler(() => _mockOpenAIHandler);
        services.AddHttpClient(); // Default HTTP client for URL fetching
        
        // Add TelemetryClient for Application Insights (disabled for tests)
        services.AddSingleton(new TelemetryClient(new TelemetryConfiguration()));
        
        // Add recipe extraction service with mock HTTP handler
        services.AddScoped<IRecipeExtractionService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var openAIClient = CreateMockOpenAIClient();
            var logger = sp.GetRequiredService<ILogger<RecipeExtractionService>>();
            var config = sp.GetRequiredService<IConfiguration>();
            var telemetryClient = sp.GetRequiredService<TelemetryClient>();
            return new RecipeExtractionService(openAIClient, config, logger, telemetryClient, httpClientFactory);
        });
        
        // Add recipe recommendation service
        services.AddScoped<IRecipeRecommendationService, RecipeRecommendationService>();
        
        // Add JWT validation service - use test implementation that validates test tokens
        services.AddSingleton<IJwtValidationService, TestJwtValidationService>();
        
        // Add authentication helper
        services.AddScoped<AuthenticationHelper>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        await Task.CompletedTask;
    }
    
    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        _mockOpenAIHandler.Dispose();
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets a service from the test service provider.
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider not initialized. Call InitializeAsync first.");
        }
        return _serviceProvider.GetRequiredService<T>();
    }
    
    /// <summary>
    /// Creates a new scope for scoped services.
    /// </summary>
    public IServiceScope CreateScope()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider not initialized. Call InitializeAsync first.");
        }
        return _serviceProvider.CreateScope();
    }
    
    /// <summary>
    /// Gets a fresh DbContext for test assertions.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        return _databaseFixture.CreateDbContext();
    }
    
    /// <summary>
    /// Resets the database to initial test state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await _databaseFixture.ResetDatabaseAsync();
    }
    
    /// <summary>
    /// Clears all blob storage.
    /// </summary>
    public async Task ClearBlobStorageAsync()
    {
        await _blobFixture.ClearContainerAsync();
    }
    
    private static Azure.AI.OpenAI.AzureOpenAIClient CreateMockOpenAIClient()
    {
        // Return a client that will be intercepted by the mock handler
        // The actual calls will go through the mock HTTP handler
        return new Azure.AI.OpenAI.AzureOpenAIClient(
            new Uri("https://test.openai.azure.com/"),
            new System.ClientModel.ApiKeyCredential("test-api-key"));
    }
}

/// <summary>
/// Combined fixture that includes database, blob storage, and test host.
/// No Docker required - uses SQLite in-memory and in-memory blob storage.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    public DatabaseFixture Database { get; } = new();
    public InMemoryBlobFixture Blobs { get; } = new();
    public FunctionsTestHost TestHost { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        // Initialize fixtures in parallel
        await Task.WhenAll(
            Database.InitializeAsync(),
            Blobs.InitializeAsync());
        
        // Create test host with initialized fixtures
        TestHost = new FunctionsTestHost(Database, Blobs);
        await TestHost.InitializeAsync();
    }
    
    public async Task DisposeAsync()
    {
        if (TestHost != null)
        {
            await TestHost.DisposeAsync();
        }
        await Task.WhenAll(
            Database.DisposeAsync(),
            Blobs.DisposeAsync());
    }
}

/// <summary>
/// xUnit collection definition for integration tests sharing fixtures.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}
