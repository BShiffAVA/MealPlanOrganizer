@metadata({
  author: 'Cloud Architecture Team'
  version: '1.0.0'
})

// ============================================================================
// PARAMETERS
// ============================================================================

@description('Azure region for resource deployment')
param location string = 'canadacentral'

@description('Environment name for tagging')
param environment string = 'prod'

@description('Function App name')
param functionAppName string = 'func-mealplan-organizer'

@description('App Service Plan name')
param appServicePlanName string = 'asp-mealplan-organizer'

@description('Storage Account name for Function App runtime')
param functionStorageAccountName string = 'stfuncmealplan'

@description('Log Analytics Workspace resource ID from Phase 1')
param logAnalyticsWorkspaceId string

@description('Virtual Network resource ID from Phase 1')
param vnetId string

@description('Integration subnet resource ID from Phase 1')
param integrationSubnetId string

@description('Application Insights resource ID from Phase 1')
param appInsightsId string

@description('Key Vault resource ID from Phase 2')
param keyVaultId string

@description('SQL Server resource ID from Phase 3')
param sqlServerId string

@description('SQL Database name from Phase 3')
param sqlDatabaseName string = 'MealPlanOrganizerDB'

@description('Storage Account resource ID from Phase 3 (recipe images)')
param storageAccountId string

@description('SignalR Service resource ID from Phase 4')
param signalRServiceId string

// ============================================================================
// VARIABLES
// ============================================================================

var tags = {
  Environment: environment
  Project: 'MealPlanOrganizer'
  Phase: '5-ComputeAppTier'
  ManagedBy: 'Bicep'
}

var keyVaultName = last(split(keyVaultId, '/'))
var storageAccountName = last(split(storageAccountId, '/'))
var signalRServiceName = last(split(signalRServiceId, '/'))

// ============================================================================
// Storage Account for Function App Runtime
// ============================================================================

resource functionStorageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: functionStorageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

// ============================================================================
// App Service Plan (Consumption Plan)
// ============================================================================

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

// ============================================================================
// Function App
// ============================================================================

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    // Note: Consumption plan (Y1) does not support VNet integration.
    // Upgrade to Premium (EP1+) to re-enable virtualNetworkSubnetId.
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      alwaysOn: false
      http20Enabled: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionStorageAccount.name};AccountKey=${functionStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionStorageAccount.name};AccountKey=${functionStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: reference(appInsightsId, '2020-02-02').InstrumentationKey
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'recommended'
        }
        // Key Vault references for secrets
        {
          name: 'SqlConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/SqlConnectionString/)'
        }
        {
          name: 'StorageConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/storage-connection-string/)'
        }
        {
          name: 'StorageAccessKey'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/storage-access-key/)'
        }
        {
          name: 'SignalRConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/SignalRConnectionString/)'
        }
        // Configuration settings
        {
          name: 'KeyVaultUrl'
          value: 'https://${keyVaultName}.vault.azure.net/'
        }
        {
          name: 'SqlDatabaseName'
          value: sqlDatabaseName
        }
        {
          name: 'StorageAccountName'
          value: storageAccountName
        }
        {
          name: 'BlobStorage__ConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/storage-connection-string/)'
        }
        {
          name: 'BlobStorage__ContainerName'
          value: 'recipe-images'
        }
        {
          name: 'SignalRServiceName'
          value: signalRServiceName
        }
        {
          name: 'Environment'
          value: environment
        }
      ]
      connectionStrings: [
        {
          name: 'Sql'
          connectionString: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/SqlConnectionString/)'
          type: 'SQLAzure'
        }
      ]
      cors: {
        allowedOrigins: [
          '*'
        ]
        supportCredentials: false
      }
      numberOfWorkers: 1
      defaultDocuments: []
      requestTracingEnabled: true
      remoteDebuggingEnabled: false
      managedPipelineMode: 'Integrated'
      virtualApplications: [
        {
          virtualPath: '/'
          physicalPath: 'site\\wwwroot'
          preloadEnabled: false
        }
      ]
      webSocketsEnabled: true
    }
  }
}

// ============================================================================
// Function App Diagnostic Settings
// ============================================================================

resource functionAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${functionAppName}-diag'
  scope: functionApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// Storage Account Diagnostic Settings for Function App Storage
// ============================================================================

resource functionStorageDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${functionStorageAccountName}-diag'
  scope: functionStorageAccount
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    metrics: [
      {
        category: 'Transaction'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// Key Vault References
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' existing = {
  name: keyVaultName
}

// ============================================================================
// RBAC Role Assignments
// ============================================================================

// Get SQL Server for role assignment
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: last(split(sqlServerId, '/'))
}

// Get SQL Database for role assignment
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' existing = {
  parent: sqlServer
  name: sqlDatabaseName
}

// Get Storage Account for role assignment
resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

// Get SignalR Service for role assignment
resource signalRService 'Microsoft.SignalRService/signalR@2024-03-01' existing = {
  name: signalRServiceName
}

// Role Assignment: Key Vault Secrets User
resource functionKeyVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, functionApp.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Role Assignment: SQL Database Contributor
resource functionSqlRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sqlDatabase
  name: guid(sqlDatabase.id, functionApp.id, 'SQL Database Contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Role Assignment: Storage Blob Data Contributor
resource functionStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, functionApp.id, 'Storage Blob Data Contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Note: SignalR Service RBAC roles vary by tenant. Configure manually if needed.

// ============================================================================
// OUTPUTS
// ============================================================================

@description('App Service Plan resource ID')
output appServicePlanId string = appServicePlan.id

@description('App Service Plan name')
output appServicePlanName string = appServicePlan.name

@description('Function App resource ID')
output functionAppId string = functionApp.id

@description('Function App name')
output functionAppName string = functionApp.name

@description('Function App hostname')
output functionAppHostname string = functionApp.properties.defaultHostName

@description('Function App default HTTPS endpoint')
output functionAppEndpoint string = 'https://${functionApp.properties.defaultHostName}'

@description('Function App Principal ID (for RBAC)')
output functionAppPrincipalId string = functionApp.identity.principalId

@description('Function App storage account name')
output functionStorageAccountName string = functionStorageAccount.name

@description('Function Storage Account ID')
output functionStorageAccountId string = functionStorageAccount.id
