# Phase 5: Compute & Application Tier - Azure Functions

## Overview

Phase 5 deploys the serverless compute infrastructure for the Meal Plan Organizer application. Azure Functions serves as the REST API backend with a Consumption Plan that automatically scales based on demand. VNet integration enables secure connectivity to private endpoints from previous phases.

## Resources Deployed

- **App Service Plan** (`asp-mealplan-organizer`)
  - Tier: Consumption (Y1)
  - Pricing: Pay-per-execution (free tier available for first 1M executions/month)
  - Auto-scaling: Automatic based on load
  
- **Function App** (`func-mealplan-organizer`)
  - Runtime: .NET 8 (isolated runtime)
  - VNet Integration: Connected to `snet-integration` subnet
  - HTTPS Only: Enabled
  - TLS Minimum Version: 1.2
  - System-assigned Managed Identity: Enabled
  
- **Function App Storage Account** (`stfuncmealplan`)
  - Tier: Standard_LRS
  - Purpose: Runtime storage for Function App state and file share
  - Access: Internal only (not user-facing)
  
- **Diagnostic Settings**
  - Function App logs and metrics → Log Analytics Workspace
  - Function storage account metrics → Log Analytics Workspace
  
- **RBAC Role Assignments** (auto-configured)
  - Key Vault Secrets User (for accessing secrets)
  - SQL Database Contributor (for database access)
  - Storage Blob Data Contributor (for recipe images)
  - SignalR Service Owner (for real-time messaging)
  - Cognitive Services OpenAI User (for recipe extraction)
  - Cognitive Services User (for AI Vision OCR)

- **GenAI Services** (for Recipe Extraction)
  - Azure OpenAI Service with GPT-4 Turbo Vision deployment
  - Azure AI Vision with Read API (OCR) capability
  - Cost: ~$1.50/month for family of 4

- **Application Settings** (Key Vault references)
  - `SqlConnectionString`: Database connection
  - `StorageConnectionString`: Blob storage connection
  - `StorageAccessKey`: Primary access key
  - `SignalRConnectionString`: Real-time messaging
  - `KeyVaultUrl`: Secrets management endpoint
  - `SqlDatabaseName`, `StorageAccountName`, `SignalRServiceName`: Configuration
  - `OpenAI__Endpoint`: Azure OpenAI service endpoint
  - `OpenAI__ApiKey`: Azure OpenAI API key
  - `OpenAI__DeploymentName`: GPT-4 Turbo deployment name
  - `Vision__Endpoint`: Azure AI Vision endpoint
  - `Vision__ApiKey`: Azure AI Vision API key

## Prerequisites

Before deploying Phase 5, ensure the following phases are completed:

- **Phase 1**: Foundation & Networking
  - Resource Group: `rg-mealplan-organizer`
  - Virtual Network: `vnet-mealplan-organizer`
  - Subnet: `snet-integration` (for Function App VNet integration)
  - Application Insights: `appi-mealplan-organizer`
  - Log Analytics Workspace: `log-mealplan-organizer`
  
- **Phase 2**: Security & Secrets Management
  - Key Vault: `kv-mealplan-org`
  - Secrets: `SqlConnectionString`, `StorageConnectionString`, `StorageAccessKey`, `SignalRConnectionString`
  
- **Phase 3**: Data Tier (Optional but recommended)
  - SQL Server: `sql-mealplan-organizer`
  - SQL Database: `MealPlanOrganizerDB`
  - Storage Account: `stmealplanorg`
  
- **Phase 4**: Real-Time Communication (Optional but recommended)
  - SignalR Service: `signalr-mealplan-organizer`

### Verify Prerequisites

```powershell
# Verify Phase 1 resources
az network vnet show --name vnet-mealplan-organizer --resource-group rg-mealplan-organizer
az network vnet subnet show --name snet-integration --vnet-name vnet-mealplan-organizer --resource-group rg-mealplan-organizer
az monitor app-insights component show --app appi-mealplan-organizer --resource-group rg-mealplan-organizer

# Verify Phase 2 resources
az keyvault show --name kv-mealplan-org --resource-group rg-mealplan-organizer
az keyvault secret list --vault-name kv-mealplan-org --query "[].name" -o tsv

# Verify Phase 3 resources (if deployed)
az sql server show --name sql-mealplan-organizer --resource-group rg-mealplan-organizer
az storage account show --name stmealplanorg --resource-group rg-mealplan-organizer

# Verify Phase 4 resources (if deployed)
az signalr show --name signalr-mealplan-organizer --resource-group rg-mealplan-organizer
```

## Deployment Instructions

### Option 1: Deploy with Azure CLI

```powershell
cd c:\dev\MealPlanOrganizer

# Deploy Phase 5
az deployment group create `
  --name phase-5-compute-tier `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-5/main.bicep `
  --parameters infra/bicep/phase-5/phase-5.parameters.json
```

### Option 2: Deploy with Custom Function Storage Account Name

```powershell
cd c:\dev\MealPlanOrganizer

# Deploy with custom storage account name (must be globally unique)
az deployment group create `
  --name phase-5-compute-tier `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-5/main.bicep `
  --parameters infra/bicep/phase-5/phase-5.parameters.json `
  --parameters functionStorageAccountName=stfunccustom12345
```

### Deployment Time

Expected deployment time: **3-5 minutes**

## Post-Deployment Verification

### 1. Verify Function App Deployment

```powershell
# Check Function App status
az functionapp show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer

# Verify HTTPS only
az functionapp config set `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --https-only true

# Get default hostname
az functionapp show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "defaultHostName" -o tsv
```

### 2. Verify VNet Integration

```powershell
# Check VNet integration configuration
az functionapp vnet-integration list `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer

# Verify subnet integration
az functionapp config show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "virtualNetworkSubnetId" -o tsv
```

### 3. Verify Managed Identity

```powershell
# Get Function App Principal ID
$principalId = az functionapp identity show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "principalId" -o tsv

Write-Host "Function App Principal ID: $principalId"

# Verify RBAC roles
az role assignment list `
  --assignee $principalId `
  --resource-group rg-mealplan-organizer `
  --output table
```

### 4. Verify Key Vault Access

```powershell
# Test Key Vault secret access
$principalId = az functionapp identity show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "principalId" -o tsv

az keyvault secret show `
  --vault-name kv-mealplan-org `
  --name SqlConnectionString `
  --query "value" -o tsv | Write-Host -ForegroundColor Green
```

### 5. Verify Application Settings

```powershell
# List all app settings
az functionapp config appsettings list `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer

# Check Key Vault references are configured
az functionapp config appsettings list `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "[?contains(value, '@Microsoft.KeyVault')].{Name:name, IsKeyVaultRef:value}" -o table
```

### 6. Verify Diagnostic Settings

```powershell
# Check Function App diagnostic settings
az monitor diagnostic-settings show `
  --name func-mealplan-organizer-diag `
  --resource $(az functionapp show --name func-mealplan-organizer --resource-group rg-mealplan-organizer --query id -o tsv)
```

### 7. Test Function App Health

```powershell
# Get Function App URL
$functionAppUrl = az functionapp show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "defaultHostName" -o tsv

# Call health endpoint
$response = Invoke-RestMethod -Uri "https://$functionAppUrl/api/health" -Method Get -ErrorAction SilentlyContinue
if ($response) {
  Write-Host "Health endpoint response: $response" -ForegroundColor Green
} else {
  Write-Host "Health endpoint not yet available (expected - no functions deployed yet)" -ForegroundColor Yellow
}
```

## Configuration Details

### Runtime Stack

**Function App Runtime**:
- Language: C#
- Runtime Stack: .NET 8 (isolated)
- Tier: Consumption Plan (Y1)

**Recommended for Development**:
- Visual Studio 2022 with Azure Functions extension
- Azure Functions Core Tools 4.x
- .NET 8 SDK

### VNet Integration

The Function App is integrated with the VNet to enable secure connectivity to private endpoints:

**Benefits**:
- Outbound traffic from Function App goes through VNet
- Can connect to SQL Database via private endpoint
- Can connect to Storage via private endpoint
- Can connect to SignalR via private endpoint
- Can connect to Key Vault via private endpoint

**Limitations**:
- Outbound internet access must go through Network Address Translation (NAT) gateway
- Private endpoint connections work seamlessly
- Cannot reach internal resources without DNS configuration

### Managed Identity Authentication

The Function App uses System-assigned Managed Identity for passwordless authentication:

**Features**:
- No credentials stored in code or configuration
- Automatic credential management by Azure
- RBAC-based access control
- Audit trail in activity logs

**Configured Roles**:
1. **Key Vault Secrets User**: Read secrets for connection strings and configuration
2. **SQL Database Contributor**: Execute queries and manage database operations
3. **Storage Blob Data Contributor**: Read/write recipe images and blobs
4. **SignalR Service Owner**: Send messages and manage connections

### Application Settings Strategy

Settings are stored in Key Vault and referenced via `@Microsoft.KeyVault()` syntax:

**Connection Strings** (from Key Vault):
```
SqlConnectionString: Server=tcp:sql-mealplan-organizer.database.windows.net,1433;Initial Catalog=MealPlanOrganizerDB;...
StorageConnectionString: BlobEndpoint=https://stmealplanorg.blob.core.windows.net;...
SignalRConnectionString: Endpoint=https://signalr-mealplan-organizer.service.signalr.net;...
```

**Configuration** (environment variables):
```
KeyVaultUrl: https://kv-mealplan-org.vault.azure.net/
SqlDatabaseName: MealPlanOrganizerDB
StorageAccountName: stmealplanorg
SignalRServiceName: signalr-mealplan-organizer
Environment: prod
```

## Cost Estimation

### Consumption Plan Pricing (Canada Central)

**Function App Executions**:
- First 1,000,000 executions/month: FREE
- After 1M: ~$0.20 per 1 million executions
- Estimated: $0/month for household use

**Function App Duration**:
- First 400,000 GB-seconds/month: FREE
- After 400K: ~$0.000016 per GB-second
- Estimated: $0/month (well within free tier)

**Storage Account (Function Runtime)**:
- Standard storage: ~$0.02/GB/month
- Estimated usage: 1-2 GB
- Cost: ~$0.02-0.04/month

**Application Insights**:
- First 1 GB/month: FREE
- Included in Phase 1 cost

**Total Estimated Cost**: **$0-0.05/month** (within free tier)

## Troubleshooting

### Issue: Function App Cannot Access Key Vault Secrets

**Symptoms**:
```
Status: 401 Unauthorized
Message: "Caller is not authorized to perform action on resource"
```

**Diagnosis**:
```powershell
# Check managed identity is enabled
az functionapp identity show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer

# Check Key Vault access policies
az keyvault show `
  --name kv-mealplan-org `
  --resource-group rg-mealplan-organizer `
  --query "properties.accessPolicies[].objectId" -o tsv
```

**Solution**:
The template automatically assigns RBAC roles. If needed, manually add access policy:
```powershell
$principalId = az functionapp identity show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "principalId" -o tsv

az keyvault set-policy `
  --name kv-mealplan-org `
  --object-id $principalId `
  --secret-permissions get list
```

### Issue: Function App Cannot Connect to SQL Database

**Symptoms**:
```
Error: "A network-related or instance-specific error occurred while establishing a connection to SQL Server"
```

**Diagnosis**:
```powershell
# Check VNet integration
az functionapp config show `
  --name func-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "virtualNetworkSubnetId" -o tsv

# Check connection string from Key Vault
az keyvault secret show `
  --vault-name kv-mealplan-org `
  --name SqlConnectionString `
  --query "value" -o tsv
```

**Solution**:
1. Verify SQL Database allows connections from Function App subnet
2. Check SQL connection string has correct database name
3. Verify SQL Server firewall allows private endpoint

### Issue: VNet Integration Failed

**Symptoms**:
```
Error: "Cannot integrate with Subnet in a different Resource Group"
```

**Solution**:
VNet and Function App must be in the same resource group. Verify:
```powershell
az network vnet show `
  --name vnet-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "resourceGroup"
```

### Issue: Function Storage Account Name Already Exists

**Symptoms**:
```
Error: "StorageAccountName already exists"
```

**Solution**:
Storage account names must be globally unique. Deploy with custom name:
```powershell
az deployment group create `
  --name phase-5-compute-tier `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-5/main.bicep `
  --parameters infra/bicep/phase-5/phase-5.parameters.json `
  --parameters functionStorageAccountName=stfunc<randomstring>
```

## Development Guidelines

### Local Development Setup

```bash
# Install Azure Functions Core Tools
choco install azure-functions-core-tools-4

# Create new function project
func init MealPlanOrganizer.Functions --dotnet-isolated

# Create new HTTP-triggered function
func new --name RecipeAPI --template "HTTP trigger"

# Run locally
func start
```

### Function Implementation Pattern

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

public static class RecipeAPI
{
    private readonly ILogger _log;
    private readonly SqlClient _sqlClient;
    private readonly BlobContainerClient _blobClient;

    public RecipeAPI(ILogger log, SqlClient sqlClient, BlobContainerClient blobClient)
    {
        _log = log;
        _sqlClient = sqlClient;
        _blobClient = blobClient;
    }

    [Function("GetRecipes")]
    public async Task<HttpResponseData> GetRecipes(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes")] HttpRequestData req)
    {
        _log.LogInformation("Getting all recipes");

        try
        {
            // Use managed identity to access SQL Database
            var recipes = await _sqlClient.GetRecipesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(recipes);
            return response;
        }
        catch (Exception ex)
        {
            _log.LogError($"Error getting recipes: {ex.Message}");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}
```

### SignalR Integration Pattern

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Azure.Messaging.SignalR;

[Function("RateRecipe")]
public async Task<HttpResponseData> RateRecipe(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recipes/{recipeId}/rate")] HttpRequestData req,
    string recipeId,
    [SignalR(HubName = "mealplan")] IAsyncCollector<SignalRMessage> signalRMessages)
{
    var rating = await req.ReadFromJsonAsync<RecipeRating>();

    // Store rating in database
    await _sqlClient.SaveRatingAsync(rating);

    // Notify household members via SignalR
    await signalRMessages.AddAsync(new SignalRMessage
    {
        Target = "RecipeRated",
        GroupName = $"household-{rating.HouseholdId}",
        Arguments = new[] { rating }
    });

    return req.CreateResponse(HttpStatusCode.OK);
}
```

## Security Best Practices

1. **Never hardcode secrets**: Always use Key Vault references with managed identity
2. **Enable HTTPS only**: Configured by default (`httpsOnly: true`)
3. **Enforce minimum TLS 1.2**: Configured by default
4. **Use RBAC for access control**: Managed identity with least-privilege roles
5. **Validate all inputs**: Implement request validation in function code
6. **Log sensitive operations**: Use Application Insights and Log Analytics
7. **Implement rate limiting**: Prevent abuse of API endpoints
8. **Use CORS carefully**: Currently allows all origins - update for production

## Next Steps

After successful Phase 5 deployment:

1. ✅ Function App deployed with VNet integration and managed identity
2. ✅ RBAC roles assigned for secure resource access
3. ✅ Application settings configured with Key Vault references
4. ✅ Diagnostic logging enabled to Log Analytics
5. ⏭️ **Develop and deploy functions**: Create API endpoints for recipe management
6. ⏭️ **Phase 6**: Configure Microsoft Entra External ID for user authentication
7. ⏭️ **Mobile App Integration**: Build .NET MAUI client and connect to Function App API

## Resources

- [Azure Functions Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
- [Consumption Plan Pricing](https://azure.microsoft.com/en-us/pricing/details/functions/)
- [VNet Integration Guide](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-vnet)
- [Managed Identity in Functions](https://learn.microsoft.com/en-us/azure/app-service/overview-managed-identity)
- [Key Vault References](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
- [Application Insights Integration](https://learn.microsoft.com/en-us/azure/azure-functions/functions-monitoring)
