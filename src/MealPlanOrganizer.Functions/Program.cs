using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.AI.OpenAI;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Configure EF Core SQL Server using ConnectionStrings:Sql via DI
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var cs = cfg.GetConnectionString("Sql");
    options.UseSqlServer(cs);
});

// Configure Azure Blob Storage clients
// For local development: uses connection string (BlobStorage:ConnectionString)
// For Azure: uses Managed Identity (DefaultAzureCredential)
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var connectionString = cfg["BlobStorage:ConnectionString"];
    var storageAccountName = cfg["StorageAccountName"];

    if (!string.IsNullOrWhiteSpace(connectionString) && connectionString.Contains("UseDevelopmentStorage"))
    {
        return new BlobServiceClient(connectionString);
    }

    if (!string.IsNullOrWhiteSpace(storageAccountName))
    {
        var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        };

        return new BlobServiceClient(blobServiceUri, new DefaultAzureCredential(credentialOptions));
    }

    throw new InvalidOperationException(
        "No blob storage configuration found. " +
        "For local: set BlobStorage:ConnectionString. For Azure: set StorageAccountName.");
});

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var containerName = cfg["BlobStorage:ContainerName"];

    if (string.IsNullOrWhiteSpace(containerName))
    {
        throw new InvalidOperationException(
            "BlobStorage:ContainerName is not configured. " +
            "Please add this setting to configuration.");
    }

    var blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
    return blobServiceClient.GetBlobContainerClient(containerName);
});

// Configure Azure OpenAI client for GenAI recipe extraction
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    
    // Try both naming conventions (double underscore for environment variables, colon for JSON)
    var endpoint = cfg["OpenAI__Endpoint"] ?? cfg["OpenAI:Endpoint"];
    var apiKey = cfg["OpenAI__ApiKey"] ?? cfg["OpenAI:ApiKey"];

    if (string.IsNullOrWhiteSpace(endpoint))
    {
        throw new InvalidOperationException(
            "OpenAI endpoint is not configured. " +
            "Please set OpenAI__Endpoint or OpenAI:Endpoint in configuration.");
    }

    // If API key is provided, use it; otherwise, try managed identity
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        return new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
    }
    
    // Use managed identity (DefaultAzureCredential) for Azure deployment
    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
});

// Configure HttpClient factory for URL fetching
builder.Services.AddHttpClient();

// Register recipe extraction service
builder.Services.AddScoped<IRecipeExtractionService, RecipeExtractionService>();

// Register recipe recommendation service for meal planning
builder.Services.AddScoped<IRecipeRecommendationService, RecipeRecommendationService>();

// Register JWT validation services for Microsoft Entra External ID
builder.Services.AddSingleton<IJwtValidationService, JwtValidationService>();
builder.Services.AddScoped<AuthenticationHelper>();

var app = builder.Build();

// Apply EF Core migrations on startup (dev/local)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
