# Phase 3: Data Tier Deployment Guide

## Overview

Phase 3 deploys the data tier components for the Meal Plan Organizer application:
- **SQL Server** with Azure AD authentication and private endpoint
- **SQL Database** (MealPlanOrganizerDB) in Basic tier with 2GB capacity
- **Storage Account** (Standard_LRS) for recipe images
- **Blob Container** (recipe-images) with private access
- **Lifecycle Management** policy for automatic cleanup after 365 days

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Meal Plan Organizer - Phase 3: Data Tier Architecture      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  SQL Server (sql-mealplan-organizer)                         │
│  ├─ Authentication: Azure AD (no SQL login)                  │
│  ├─ Database: MealPlanOrganizerDB (Basic tier, 2GB)          │
│  ├─ Private Endpoint: snet-private-endpoints                 │
│  ├─ DNS: privatelink.database.windows.net                    │
│  └─ Monitoring: Log Analytics diagnostics                    │
│                                                               │
│  Storage Account (stmealplanorg)                             │
│  ├─ SKU: Standard_LRS (geo-redundant reads)                  │
│  ├─ Blob Services: Enabled                                   │
│  │  ├─ Container: recipe-images (Private)                    │
│  │  └─ Lifecycle: Delete after 365 days                      │
│  ├─ Private Endpoint: snet-private-endpoints                 │
│  ├─ DNS: privatelink.blob.core.windows.net                   │
│  ├─ Network Access: Disabled public access                   │
│  └─ Monitoring: Log Analytics diagnostics                    │
│                                                               │
│  Key Vault (from Phase 2)                                    │
│  ├─ Secret: sql-connection-string                            │
│  ├─ Secret: sql-admin-password                               │
│  ├─ Secret: storage-connection-string                        │
│  └─ Secret: storage-access-key                               │
│                                                               │
│  Log Analytics (from Phase 1)                                │
│  └─ Receives diagnostics from SQL and Storage                │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Prerequisites

### Required
- **Phase 1** must be deployed (Networking & VNet infrastructure)
  - VNet: vnet-mealplan-organizer
  - Subnet: snet-private-endpoints
  - Private DNS Zones: privatelink.database.windows.net, privatelink.blob.core.windows.net
  - Log Analytics Workspace: log-mealplan-organizer

- **Phase 2** must be deployed (Security & Key Vault)
  - Key Vault: kv-mealplan-org
  - Key Vault Private Endpoint (already connected to VNet)

- **Azure CLI** installed (version 2.50+)
- **Bicep CLI** installed (or Azure CLI with Bicep support)
- **Permissions**:
  - Contributor role on the subscription
  - User Access Administrator role (for Azure AD authentication setup)
  - Access to retrieve Azure AD service principal object ID

### Azure AD Service Principal Setup
```powershell
# Get the Azure AD service principal for SQL Server authentication
$adPrincipal = Get-AzADServicePrincipal -Filter "displayName eq 'mealplan-sql-admin'" -First 1
if (-not $adPrincipal) {
  Write-Host "Creating Azure AD service principal for SQL Server authentication..."
  $adPrincipal = New-AzADServicePrincipal -DisplayName "mealplan-sql-admin" -SkipAssignment
  Write-Host "Created service principal with ObjectId: $($adPrincipal.Id)"
}
$sqlAadAdminObjectId = $adPrincipal.Id
Write-Host "SQL AAD Admin ObjectId: $sqlAadAdminObjectId"
```

## Deployment Instructions

### Step 1: Prepare Parameters

Update `phase-3.parameters.json` with your Azure AD service principal object ID:

```bash
# Get the service principal object ID
$adObjectId = (Get-AzADServicePrincipal -Filter "displayName eq 'mealplan-sql-admin'" -First 1).Id
echo "SQL AAD Admin ObjectId: $adObjectId"

# Update the parameters file
$params = Get-Content 'phase-3.parameters.json' | ConvertFrom-Json
$params.parameters.sqlAadAdminObjectId.value = $adObjectId
$params | ConvertTo-Json -Depth 10 | Set-Content 'phase-3.parameters.json'
```

### Step 2: Validate Bicep Templates

```bash
# Navigate to the phase-3 directory
cd infra/bicep/phase-3

# Validate the bicep template
az bicep build --file main.bicep --validate-only

# Or using bicep CLI directly
bicep build main.bicep
```

### Step 3: Deploy to Azure

```bash
# Set variables
$subscriptionId = "46c4ffee-60f3-4055-813c-3037475e62ca"
$resourceGroupName = "rg-mealplan-organizer"
$location = "canadacentral"
$deploymentName = "phase-3-$(Get-Date -Format 'yyyyMMddHHmmss')"

# Login to Azure (if not already authenticated)
az login
az account set --subscription $subscriptionId

# Create resource group (if it doesn't exist)
az group create \
  --name $resourceGroupName \
  --location $location

# Deploy using bicep template
az deployment group create \
  --resource-group $resourceGroupName \
  --template-file main.bicep \
  --parameters @phase-3.parameters.json \
  --parameters location=$location \
  --name $deploymentName

# Verify deployment
$deployment = az deployment group show \
  --name $deploymentName \
  --resource-group $resourceGroupName \
  --query "{state: properties.provisioningState, outputs: properties.outputs}" \
  --output json

Write-Host "Deployment State: $($deployment.state)"
Write-Host "Outputs: $($deployment.outputs | ConvertTo-Json)"
```

### Step 4: Verify Resources

```bash
# Check SQL Server
az sql server show \
  --name sql-mealplan-organizer \
  --resource-group rg-mealplan-organizer

# Check SQL Database
az sql db show \
  --name MealPlanOrganizerDB \
  --server sql-mealplan-organizer \
  --resource-group rg-mealplan-organizer

# Check Storage Account
az storage account show \
  --name stmealplanorg \
  --resource-group rg-mealplan-organizer

# Verify private endpoints
az network private-endpoint list \
  --resource-group rg-mealplan-organizer \
  --output table
```

### Step 5: Retrieve Connection Strings

```bash
# Get SQL connection string from Key Vault
az keyvault secret show \
  --vault-name kv-mealplan-org \
  --name sql-connection-string \
  --query value \
  --output tsv

# Get Storage connection string from Key Vault
az keyvault secret show \
  --vault-name kv-mealplan-org \
  --name storage-connection-string \
  --query value \
  --output tsv

# Get Storage access key from Key Vault
az keyvault secret show \
  --vault-name kv-mealplan-org \
  --name storage-access-key \
  --query value \
  --output tsv
```

## Resource Configuration Details

### SQL Server Configuration
- **Name**: sql-mealplan-organizer
- **Location**: Canada Central
- **Tier**: SQL Server (PaaS)
- **Authentication**: Azure AD only (no SQL login)
- **TLS Version**: 1.2 minimum
- **Public Network Access**: Disabled (private endpoint only)
- **Outbound Network Restrictions**: Disabled (for Azure services)

### SQL Database Configuration
- **Name**: MealPlanOrganizerDB
- **SKU**: Basic (DTU-based)
- **Max Size**: 2GB (2147483648 bytes)
- **Collation**: SQL_Latin1_General_CP1_CI_AS
- **Zone Redundancy**: Disabled (Basic tier doesn't support)
- **Geo-Replication**: Enabled (Standard geo-replication)

### Storage Account Configuration
- **Name**: stmealplanorg (must be globally unique)
- **Location**: Canada Central
- **Account Kind**: StorageV2 (supports blob, files, tables, queues)
- **SKU**: Standard_LRS (Locally Redundant Storage)
- **Tier**: Hot (optimized for frequent access)
- **TLS Version**: 1.2 minimum
- **Shared Key Access**: Disabled (use Azure AD/managed identity)
- **Public Access**: Disabled
- **Cross-Tenant Replication**: Disabled

### Blob Container Configuration
- **Name**: recipe-images
- **Public Access**: Private (no anonymous access)
- **Versioning**: Disabled
- **Soft Delete**: Enabled (7-day retention)
- **Change Feed**: Enabled (for audit/compliance)

### Lifecycle Management Policy
- **Rule Name**: DeleteOldBlobs
- **Action**: Delete
- **Trigger**: Blobs and snapshots with LastModifiedTime > 365 days
- **Applies To**: All block blobs and append blobs

### Diagnostic Settings
**SQL Server Logs**
- SQLSecurityAuditEvents
- Errors
- Metrics: Basic (1-minute granularity)

**Storage Account Logs**
- StorageRead
- StorageWrite
- StorageDelete
- Metrics: Transaction (1-minute granularity)

All logs and metrics route to the Phase 1 Log Analytics Workspace for centralized monitoring.

## Security Considerations

### Network Isolation
- ✅ Private endpoints isolate both SQL Server and Storage Account from public internet
- ✅ Private DNS zones automatically resolve to private IP addresses
- ✅ All traffic flows through the VNet with Network Security Group (NSG) rules

### Authentication & Authorization
- ✅ SQL Server: Azure AD only (managed identities for application access)
- ✅ Storage Account: Shared key access disabled (use managed identities or Azure AD)
- ✅ Key Vault: RBAC-based access control (from Phase 2)

### Data Protection
- ✅ TLS 1.2 enforcement for all connections
- ✅ Encryption at rest (Microsoft-managed keys)
- ✅ Encryption in transit (HTTPS only)
- ✅ Blob soft delete enabled (7-day retention before permanent deletion)
- ✅ Audit logging to Log Analytics for compliance

### Compliance
- ✅ All resources in same region (Canada Central) for data residency
- ✅ Automatic backup for SQL Database (24-hour retention at Basic tier)
- ✅ Automatic cleanup of old blobs (365-day lifecycle policy)

## Cost Estimation (Canada Central)

### SQL Server Costs
- **SQL Server Compute**: $5.40/month (Basic tier)
  - Basic tier: 5 DTU compute
  - Includes automatic backups
  
- **SQL Database Storage**: $0.50/month (2GB at $0.25/GB)
  - Data storage only (backups are included)

**SQL Server Monthly Total**: ~$5.90

### Storage Account Costs
- **Standard Storage (LRS)**: $0.023/GB/month
  - Assumed 10GB initial usage: $0.23/month
  
- **Operations**: Negligible (<$0.10/month)
  - Read operations: $0.01 per 10,000 requests
  - Write operations: $0.05 per 10,000 requests

**Storage Account Monthly Total**: ~$0.33 (scales with usage)

### Log Analytics Costs
- Included in Phase 1 (Pay-As-You-Go ingestion)
- Estimated: $0.50-$1.00/month for combined Phase 1-3 diagnostics

### Private Endpoints
- No additional cost for private endpoints themselves
- Included in VNet infrastructure cost

**Phase 3 Total Estimated Monthly Cost**: ~$6.70

## Troubleshooting

### Issue 1: Private Endpoint DNS Resolution Fails

**Symptoms**
```
Error: Unable to resolve hostname 'sql-mealplan-organizer.database.windows.net'
```

**Diagnosis**
```bash
# Check if private DNS zone is linked to VNet
az network private-dns link vnet list \
  --zone-name privatelink.database.windows.net \
  --resource-group rg-mealplan-organizer

# Verify DNS records in private zone
az network private-dns record-set list \
  --zone-name privatelink.database.windows.net \
  --resource-group rg-mealplan-organizer
```

**Resolution**
1. Ensure Phase 1 deployment included private DNS zones
2. Verify the private DNS zone is linked to vnet-mealplan-organizer
3. Check that private endpoint creation completed successfully
4. From a VM in the same VNet, try `nslookup sql-mealplan-organizer.privatelink.database.windows.net`

### Issue 2: Storage Account Blob Access Denied

**Symptoms**
```
Error: This request is not authorized to perform this operation.
403 Forbidden
```

**Diagnosis**
```bash
# Check storage account network rules
az storage account show \
  --name stmealplanorg \
  --resource-group rg-mealplan-organizer \
  --query networkRuleSet

# Check if public endpoint is accessible
curl -v https://stmealplanorg.blob.core.windows.net/recipe-images
```

**Resolution**
1. Ensure you're accessing from within the VNet or through the private endpoint
2. Use Azure AD authentication or stored access keys from Key Vault
3. If accessing from outside the VNet, add client IP to storage account network rules:
   ```bash
   az storage account network-rule add \
     --account-name stmealplanorg \
     --ip-address <your-public-ip>
   ```

### Issue 3: SQL Authentication with Azure AD Fails

**Symptoms**
```
Error: User account 'mealplan-sql-admin' not found in directory
```

**Diagnosis**
```bash
# Verify Azure AD principal exists
Get-AzADServicePrincipal -Filter "displayName eq 'mealplan-sql-admin'"

# Check SQL Server Azure AD admin settings
az sql server ad-admin show \
  --resource-group rg-mealplan-organizer \
  --server-name sql-mealplan-organizer
```

**Resolution**
1. Create the Azure AD service principal if it doesn't exist:
   ```bash
   New-AzADServicePrincipal -DisplayName "mealplan-sql-admin"
   ```
2. Get the object ID and update parameters.json
3. Re-deploy Phase 3 with the correct object ID
4. For testing, use Azure Portal SQL query editor with Azure AD authentication

### Issue 4: Deployment Timeout or Failure

**Symptoms**
```
Error: Deployment failed. Resource creation timed out or returned error status.
```

**Diagnosis**
```bash
# Check deployment operations
az deployment operation group list \
  --resource-group rg-mealplan-organizer \
  --name <deployment-name> \
  --query "[].{operation: operationId, status: properties.provisioningState, statusMessage: properties.statusMessage}" \
  --output table

# Check resource creation status
az resource list \
  --resource-group rg-mealplan-organizer \
  --output table
```

**Resolution**
1. Review Azure Activity Log for detailed error messages
2. Check Key Vault permissions (may be blocking secret creation)
3. Verify all Phase 1 and Phase 2 resources are deployed and accessible
4. Retry deployment with verbose output:
   ```bash
   az deployment group create \
     --verbose \
     --resource-group rg-mealplan-organizer \
     --template-file main.bicep \
     --parameters @phase-3.parameters.json
   ```

## Testing & Verification

### Test 1: SQL Database Connectivity

```powershell
# Install SqlServer module if needed
Install-Module -Name SqlServer -Force

# Connect to SQL Database using Azure AD
$serverName = "sql-mealplan-organizer.database.windows.net"
$databaseName = "MealPlanOrganizerDB"
$userId = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value

# Connect and run test query
$connection = New-Object System.Data.SqlClient.SqlConnection
$connection.ConnectionString = "Server=$serverName;Database=$databaseName;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;Authentication='Active Directory Integrated';"
$connection.Open()

$command = $connection.CreateCommand()
$command.CommandText = "SELECT @@VERSION as SQLVersion"
$result = $command.ExecuteScalar()
Write-Host "SQL Server Version: $result"

$connection.Close()
```

### Test 2: Blob Upload from Private Network

```powershell
# From a VM in the same VNet
$storageAccountName = "stmealplanorg"
$containerName = "recipe-images"
$blobName = "test-image.txt"
$fileContent = "Test blob content from private network"

# Get storage account connection string from Key Vault
$connString = az keyvault secret show --vault-name kv-mealplan-org --name storage-connection-string --query value -o tsv

# Upload blob using PowerShell
$context = New-AzStorageContext -ConnectionString $connString
Set-AzStorageBlobContent `
  -Container $containerName `
  -File "test-image.txt" `
  -Blob $blobName `
  -Context $context

Write-Host "Blob uploaded successfully: $blobName"
```

### Test 3: Verify Lifecycle Policy

```bash
# Check blob properties to confirm aging calculation
az storage blob show \
  --account-name stmealplanorg \
  --container-name recipe-images \
  --name <blob-name> \
  --query properties
```

### Test 4: Verify Diagnostic Logs

```bash
# Query Log Analytics for SQL diagnostics
az monitor log-analytics query \
  --workspace-id <log-analytics-workspace-id> \
  --analytics-query "AzureDiagnostics | where ResourceProvider == 'MICROSOFT.SQL' | take 10"

# Query Log Analytics for Storage diagnostics
az monitor log-analytics query \
  --workspace-id <log-analytics-workspace-id> \
  --analytics-query "StorageBlobLogs | take 10"
```

## Next Steps

After Phase 3 deployment is verified:

1. **Phase 4 - SignalR Service**: Deploy Azure SignalR Service for real-time notifications
2. **Phase 5 - Compute Tier**: Deploy Azure Functions and App Service Plan
3. **Phase 6 - Authentication**: Configure Microsoft Entra External ID in Azure Portal
4. **End-to-End Testing**: Test full application flow with all components deployed

## File Structure

```
infra/bicep/phase-3/
├── main.bicep                    # SQL Server and Storage Account definitions
├── phase-3.parameters.json       # Deployment parameters
└── README.md                      # This file
```

## Support & Monitoring

**Azure Portal Monitoring**
- SQL Server: Monitor DTU usage, database size, and query performance
- Storage Account: Monitor transaction volume, data consumption, and access patterns
- Log Analytics: Query diagnostic logs for SQL errors and storage access patterns

**Common Monitoring Queries**
```kusto
// SQL Server errors in the last 24 hours
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.SQL"
| where Category == "Errors"
| where TimeGenerated > ago(24h)
| project TimeGenerated, ErrorCode = OperationName, ErrorMessage = tostring(Details)

// Storage blob access patterns
StorageBlobLogs
| where TimeGenerated > ago(24h)
| where OperationName in ("GetBlob", "PutBlob", "DeleteBlob")
| summarize count() by OperationName
```

## Cleanup

To remove Phase 3 resources without affecting earlier phases:

```bash
# Delete specific resources
az resource delete \
  --resource-group rg-mealplan-organizer \
  --ids "/subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Sql/servers/sql-mealplan-organizer"

az resource delete \
  --resource-group rg-mealplan-organizer \
  --ids "/subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Storage/storageAccounts/stmealplanorg"
```

**Note**: Key Vault secrets created in Phase 3 may need manual deletion if purge protection is enabled.
