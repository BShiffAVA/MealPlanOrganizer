using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MealPlanOrganizer.Functions.Data;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using Azure.Identity;

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

// Configure Azure Blob Storage client
// For local development: uses connection string (BlobStorage:ConnectionString)
// For Azure: uses Managed Identity (DefaultAzureCredential)
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var connectionString = cfg["BlobStorage:ConnectionString"];
    var containerName = cfg["BlobStorage:ContainerName"];
    var storageAccountName = cfg["StorageAccountName"];
    
    if (string.IsNullOrWhiteSpace(containerName))
    {
        throw new InvalidOperationException(
            "BlobStorage:ContainerName is not configured. " +
            "Please add this setting to configuration.");
    }
    
    BlobContainerClient containerClient;
    
    // Check if we're in local development (connection string with UseDevelopmentStorage)
    if (!string.IsNullOrWhiteSpace(connectionString) && connectionString.Contains("UseDevelopmentStorage"))
    {
        // Local development: use connection string
        var blobServiceClient = new BlobServiceClient(connectionString);
        containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }
    else if (!string.IsNullOrWhiteSpace(storageAccountName))
    {
        // Azure deployment: use Managed Identity
        var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        };
        
        var blobServiceClient = new BlobServiceClient(blobServiceUri, new DefaultAzureCredential(credentialOptions));
        containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }
    else
    {
        throw new InvalidOperationException(
            "No blob storage configuration found. " +
            "For local: set BlobStorage:ConnectionString. For Azure: set StorageAccountName.");
    }
    
    return containerClient;
});
var app = builder.Build();

// Apply EF Core migrations on startup (dev/local)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
