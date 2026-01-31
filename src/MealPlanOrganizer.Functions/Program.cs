using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MealPlanOrganizer.Functions.Data;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;

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
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var connectionString = cfg["BlobStorage:ConnectionString"];
    var containerName = cfg["BlobStorage:ContainerName"];
    var blobServiceClient = new BlobServiceClient(connectionString);
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
