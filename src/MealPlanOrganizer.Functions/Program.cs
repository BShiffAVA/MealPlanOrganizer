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

// Configure Azure Blob Storage client using Managed Identity
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var storageAccountName = cfg["StorageAccountName"];
    var containerName = cfg["BlobStorage:ContainerName"];
    
    if (string.IsNullOrWhiteSpace(storageAccountName))
    {
        throw new InvalidOperationException(
            "StorageAccountName is not configured. " +
            "Please add this setting to Azure Function App Configuration.");
    }
    
    if (string.IsNullOrWhiteSpace(containerName))
    {
        throw new InvalidOperationException(
            "BlobStorage:ContainerName (or BlobStorage__ContainerName) is not configured. " +
            "Please add this setting to Azure Function App Configuration.");
    }
    
    // Use Managed Identity (DefaultAzureCredential) for authentication
    // This works with the Storage Blob Data Contributor role assigned in Bicep
    var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
    var blobServiceClient = new BlobServiceClient(blobServiceUri, new DefaultAzureCredential());
    return blobServiceClient.GetBlobContainerClient(containerName);
});
var app = builder.Build();

// Apply EF Core migrations on startup (dev/local)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
