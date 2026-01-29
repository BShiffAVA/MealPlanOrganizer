# Phase 4: Real-Time Communication - Azure SignalR Service

## Overview

Phase 4 deploys the real-time communication infrastructure for the Meal Plan Organizer application. Azure SignalR Service enables push notifications to mobile clients when family members update recipes, add ratings, or modify meal plans.

## Resources Deployed

- **Azure SignalR Service** (`signalr-mealplan-organizer`)
  - Tier: Free (Free_F1)
  - Capacity: 1 unit (20 concurrent connections)
  - Service Mode: Serverless
  - Network: Private endpoint only, public access disabled
  - Features: Connectivity and messaging logs enabled
  
- **Private Endpoint** (`signalr-mealplan-organizer-pep-[hash]`)
  - Subnet: snet-private-endpoints (from Phase 1)
  - DNS Zone: privatelink.service.signalr.net (from Phase 1)
  
- **Diagnostic Settings** (`signalr-mealplan-organizer-diag`)
  - Destination: Log Analytics Workspace (from Phase 1)
  - Logs: All log categories
  - Metrics: AllMetrics
  
- **Key Vault Secrets** (stored in kv-mealplan-org from Phase 2)
  - `SignalRConnectionString`: Full connection string with access key
  - `SignalRPrimaryKey`: Primary access key for authentication
  - `SignalREndpoint`: HTTPS endpoint URL

## Prerequisites

Before deploying Phase 4, ensure the following phases are completed:

- **Phase 1**: Foundation & Networking
  - Resource Group: `rg-mealplan-organizer`
  - Virtual Network: `vnet-mealplan-organizer`
  - Subnet: `snet-private-endpoints`
  - Private DNS Zone: `privatelink.service.signalr.net`
  - Log Analytics Workspace: `log-mealplan-organizer`
  
- **Phase 2**: Security & Secrets Management
  - Key Vault: `kv-mealplan-org`

### Verify Prerequisites

```powershell
# Verify Phase 1 resources
az network vnet show --name vnet-mealplan-organizer --resource-group rg-mealplan-organizer
az network vnet subnet show --name snet-private-endpoints --vnet-name vnet-mealplan-organizer --resource-group rg-mealplan-organizer
az network private-dns zone show --name privatelink.service.signalr.net --resource-group rg-mealplan-organizer
az monitor log-analytics workspace show --workspace-name log-mealplan-organizer --resource-group rg-mealplan-organizer

# Verify Phase 2 resources
az keyvault show --name kv-mealplan-org --resource-group rg-mealplan-organizer
```

## Deployment Instructions

### Option 1: Deploy with Azure CLI

```powershell
cd c:\dev\MealPlanOrganizer

# Deploy Phase 4
az deployment group create `
  --name phase-4-realtime `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-4/main.bicep `
  --parameters infra/bicep/phase-4/phase-4.parameters.json
```

### Option 2: Deploy with Custom Parameters

```powershell
cd c:\dev\MealPlanOrganizer

# Deploy with custom SignalR SKU (Standard instead of Free)
az deployment group create `
  --name phase-4-realtime `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-4/main.bicep `
  --parameters infra/bicep/phase-4/phase-4.parameters.json `
  --parameters signalRSku=Standard_S1 signalRCapacity=1
```

### Deployment Time

Expected deployment time: **5-7 minutes**

## Post-Deployment Verification

### 1. Verify SignalR Service

```powershell
# Check SignalR Service status
az signalr show `
  --name signalr-mealplan-organizer `
  --resource-group rg-mealplan-organizer

# Verify service mode is Serverless
az signalr show `
  --name signalr-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "features[?flag=='ServiceMode'].value" -o tsv
```

### 2. Verify Private Endpoint

```powershell
# List private endpoints
az network private-endpoint list `
  --resource-group rg-mealplan-organizer `
  --query "[?contains(name, 'signalr')].{Name:name, ProvisioningState:provisioningState}" -o table

# Check private endpoint connection
az signalr show `
  --name signalr-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --query "privateEndpointConnections[].{Name:name, Status:privateLinkServiceConnectionState.status}" -o table
```

### 3. Verify Key Vault Secrets

```powershell
# List SignalR-related secrets
az keyvault secret list `
  --vault-name kv-mealplan-org `
  --query "[?contains(name, 'SignalR')].{Name:name, Enabled:attributes.enabled}" -o table

# Retrieve connection string (masked)
az keyvault secret show `
  --vault-name kv-mealplan-org `
  --name SignalRConnectionString `
  --query "value" -o tsv | Write-Host -ForegroundColor Green
```

### 4. Test DNS Resolution

```powershell
# Resolve SignalR private endpoint (requires VM in the VNet or Azure Bastion)
# This command should resolve to a private IP (10.0.x.x)
nslookup signalr-mealplan-organizer.service.signalr.net
```

### 5. Verify Diagnostic Settings

```powershell
# Check diagnostic settings
az monitor diagnostic-settings show `
  --name signalr-mealplan-organizer-diag `
  --resource $(az signalr show --name signalr-mealplan-organizer --resource-group rg-mealplan-organizer --query id -o tsv)
```

## Configuration Details

### SignalR Service Mode: Serverless

The SignalR Service is configured in **Serverless mode**, which is optimal for the Meal Plan Organizer use case:

**Benefits:**
- **Cost-Effective**: Pay only for messages sent (no standby charge)
- **Azure Functions Integration**: Seamless integration with Function App (Phase 5)
- **Auto-Scaling**: Automatically handles connection scaling
- **Low Concurrency**: Perfect for 4-person household (20 concurrent connections on Free tier)

**Serverless Mode Requirements:**
- Function App must use SignalR bindings (configured in Phase 5)
- Clients connect through Functions, not directly to SignalR
- Connection lifetimes managed by Azure Functions

### Network Security

**Public Network Access**: Disabled
- All connections must go through private endpoint
- Public internet cannot access SignalR Service

**Network ACLs**:
- Default Action: Deny
- Private Endpoint: Allow ServerConnection, ClientConnection, RESTAPI
- Public Network: Deny all traffic

### CORS Configuration

CORS is configured to allow all origins (`*`) for development. For production:

```powershell
# Update CORS to specific domains
az signalr cors update `
  --name signalr-mealplan-organizer `
  --resource-group rg-mealplan-organizer `
  --allowed-origins "https://yourdomain.com" "https://www.yourdomain.com"
```

## Cost Estimation

### Free Tier (Free_F1)
- **Price**: $0/month
- **Included**: 
  - 20 concurrent connections
  - 20,000 messages per day
- **Limits**: Cannot scale beyond Free tier capacity
- **Recommended For**: Development, testing, small household use (4-5 users)

### Standard Tier (Standard_S1)
- **Price**: ~$40/month (1 unit)
- **Included**: 
  - 1,000 concurrent connections per unit
  - 1,000,000 messages per day per unit
- **Scalability**: Can scale to 100 units
- **Recommended For**: Production with multiple households or high message volume

**For Meal Plan Organizer (4 users):**
- Estimated Messages: ~500-1,000 per day (ratings, meal plan updates)
- Recommended: **Free_F1** tier (well within limits)

## Troubleshooting

### Issue: Private Endpoint in Disconnected State

**Symptoms:**
```
PrivateEndpointCannotBeUpdatedInDisconnectedState
```

**Solution:**
1. Delete the failed private endpoint:
```powershell
az network private-endpoint delete `
  --name signalr-mealplan-organizer-pep-[hash] `
  --resource-group rg-mealplan-organizer
```

2. Re-deploy Phase 4 (new unique suffix will be generated)

### Issue: Key Vault Access Denied

**Symptoms:**
```
The user, group or application 'object-id' does not have secrets set permission
```

**Solution:**
Grant Key Vault Secrets Officer role to your user account:
```powershell
az role assignment create `
  --assignee your.email@domain.com `
  --role "Key Vault Secrets Officer" `
  --scope /subscriptions/46c4ffee-60f3-4055-813c-3037475e62ca/resourceGroups/rg-mealplan-organizer/providers/Microsoft.KeyVault/vaults/kv-mealplan-org
```

### Issue: SignalR Connection Failed from Function App

**Symptoms:**
Function App cannot connect to SignalR Service

**Diagnosis:**
```powershell
# Check if Function App managed identity has SignalR Service Owner role
az role assignment list `
  --scope $(az signalr show --name signalr-mealplan-organizer --resource-group rg-mealplan-organizer --query id -o tsv) `
  --query "[?principalType=='ServicePrincipal'].{PrincipalId:principalId, Role:roleDefinitionName}" -o table
```

**Solution:** (Will be configured in Phase 5)
```powershell
# Grant SignalR Service Owner role to Function App (replace <function-principal-id>)
az role assignment create `
  --assignee <function-principal-id> `
  --role "SignalR Service Owner" `
  --scope $(az signalr show --name signalr-mealplan-organizer --resource-group rg-mealplan-organizer --query id -o tsv)
```

### Issue: Messages Not Being Delivered

**Symptoms:**
SignalR messages sent but clients not receiving them

**Diagnosis:**
1. Check SignalR diagnostic logs in Log Analytics:
```kusto
SignalRServiceDiagnosticLogs
| where TimeGenerated > ago(1h)
| where Level == "Error"
| project TimeGenerated, OperationName, Message
```

2. Verify client connection state:
```kusto
SignalRServiceDiagnosticLogs
| where OperationName == "ClientConnected" or OperationName == "ClientDisconnected"
| summarize ConnectionCount = count() by OperationName, bin(TimeGenerated, 5m)
```

## Integration with Function App (Phase 5)

Phase 5 will configure the Function App to use SignalR with the following steps:

1. **Install NuGet Package**: `Microsoft.Azure.WebJobs.Extensions.SignalRService`

2. **Configure App Settings** (from Key Vault):
   - `AzureSignalRConnectionString`: `@Microsoft.KeyVault(SecretUri=https://kv-mealplan-org.vault.azure.net/secrets/SignalRConnectionString/)`

3. **Grant Managed Identity Access**:
   - Assign `SignalR Service Owner` role to Function App managed identity

4. **Sample Function Code** (Recipe Rating Notification):
```csharp
[FunctionName("SendRatingNotification")]
public static async Task Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
    [SignalR(HubName = "mealplan")] IAsyncCollector<SignalRMessage> signalRMessages)
{
    // Parse request body
    var rating = await req.ReadFromJsonAsync<RecipeRating>();
    
    // Send SignalR message to household group
    await signalRMessages.AddAsync(new SignalRMessage
    {
        Target = "RecipeRated",
        GroupName = $"household-{rating.HouseholdId}",
        Arguments = new[] { rating }
    });
}
```

## Security Best Practices

1. **Use Managed Identity**: Never store SignalR keys in code or appsettings.json
2. **Household Isolation**: Use SignalR groups to isolate messages between households
3. **Message Validation**: Validate all message payloads in Function App before broadcasting
4. **Rate Limiting**: Implement rate limiting in Function App to prevent message spam
5. **Audit Logs**: Regularly review SignalR diagnostic logs in Log Analytics

## Next Steps

After successful Phase 4 deployment:

1. ✅ SignalR Service deployed and accessible via private endpoint
2. ✅ Connection details stored securely in Key Vault
3. ⏭️ **Phase 5**: Deploy Azure Functions App Service Plan and Function App
   - Configure VNet integration
   - Set up managed identity authentication
   - Install SignalR bindings
   - Grant RBAC roles for SignalR access

## Resources

- [Azure SignalR Service Documentation](https://learn.microsoft.com/en-us/azure/azure-signalr/)
- [SignalR Service Serverless Mode](https://learn.microsoft.com/en-us/azure/azure-signalr/signalr-concept-serverless-development-config)
- [Azure Functions SignalR Bindings](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-signalr-service)
- [SignalR Service Pricing](https://azure.microsoft.com/en-us/pricing/details/signalr-service/)
- [Private Endpoints for SignalR](https://learn.microsoft.com/en-us/azure/azure-signalr/howto-private-endpoints)
