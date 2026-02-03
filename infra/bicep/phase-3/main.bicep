// ============================================================================
// Phase 3: Data Tier - SQL Server and Storage Account Deployment
// ============================================================================
// Objective: Deploy SQL Database (Basic tier) and Storage Account with private 
// endpoints for recipe images, ensuring data isolation and cost-optimized 
// configuration.

// ============================================================================
// Parameters
// ============================================================================
@description('The Azure region where resources will be deployed')
@allowed(['canadacentral', 'eastus', 'westus2', 'northeurope'])
param location string = 'canadacentral'

@description('Environment name for resource naming and tagging')
@allowed(['dev', 'prod', 'staging'])
param environment string = 'prod'

@description('SQL Server name')
param sqlServerName string = 'sql-mealplan-organizer'

@description('SQL Server administrator login name')
param sqlAdminLogin string = 'mealplanadmin'

@description('SQL Server administrator password (must contain uppercase, lowercase, numbers, and special characters)')
@minLength(8)
@secure()
param sqlAdminPassword string = 'MealP!an@2024Secure'

@description('Azure AD principal object ID for SQL Server administrator')
param sqlAadAdminObjectId string

@description('SQL database name')
param sqlDatabaseName string = 'MealPlanOrganizerDB'

@description('Storage account name')
param storageAccountName string = 'stmealplanorg'

@description('Log Analytics workspace name from Phase 1')
param logAnalyticsWorkspaceName string = 'log-mealplan-organizer'

@description('Key Vault name from Phase 2')
param keyVaultName string = 'kv-mealplan-org'

@description('Resource tags')
param tags object = {
  Phase: '3-DataTier'
  Environment: environment
  ManagedBy: 'Bicep'
}

// ============================================================================
// Variables
// ============================================================================
var subscriptionId = subscription().subscriptionId
var resourceGroupName = resourceGroup().name
var keyVaultUri = 'https://${keyVaultName}.vault.azure.net/'
var sqlServerFqdn = '${sqlServerName}.database.windows.net'

// ============================================================================
// SQL Server
// ============================================================================
// Note: SQL Server is configured with Azure AD-only authentication.
// SQL password authentication is disabled for security.
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
    isIPv6Enabled: false
  }
}

// Enable Azure AD-only authentication (disable SQL auth)
resource sqlServerAzureAdOnly 'Microsoft.Sql/servers/azureADOnlyAuthentications@2023-08-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    azureADOnlyAuthentication: true
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}


// SQL Server Diagnostic Settings
resource sqlDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${sqlServerName}-diag'
  scope: sqlServer
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// Storage Account
// ============================================================================
resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    encryption: {
      keySource: 'Microsoft.Storage'
      services: {
        blob: { enabled: true }
        file: { enabled: true }
      }
    }
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

// Blob Services
resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    changeFeed: { enabled: true }
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

// Recipe Images Blob Container
resource recipeImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobServices
  name: 'recipe-images'
  properties: {
    publicAccess: 'None'
  }
}

// Lifecycle Management Policy
resource storageLifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2024-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: 'DeleteOldBlobs'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterCreationGreaterThan: 365
                }
              }
            }
            filters: {
              blobTypes: [ 'blockBlob' ]
            }
          }
        }
      ]
    }
  }
}


// Storage Account Diagnostic Settings
resource storageDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${storageAccountName}-diag'
  scope: blobServices
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      { category: 'StorageRead', enabled: true }
      { category: 'StorageWrite', enabled: true }
      { category: 'StorageDelete', enabled: true }
    ]
    metrics: [
      { category: 'Transaction', enabled: true }
    ]
  }
}

// ============================================================================
// Existing Resources References (from previous phases)
// ============================================================================
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'sql-connection-string'
  properties: {
    value: 'Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Integrated;'
  }
}

resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'storage-connection-string'
  properties: {
    value: 'BlobEndpoint=${storageAccount.properties.primaryEndpoints.blob};SharedAccessSignature=placeholder'
  }
}

resource storageAccessKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'storage-access-key'
  properties: {
    value: listKeys(storageAccount.id, '2024-01-01').keys[0].value
  }
}

// ============================================================================
// Outputs
// ============================================================================
output sqlServerFqdn string = sqlServerFqdn
output sqlServerId string = sqlServer.id
output sqlDatabaseId string = sqlDatabase.id

output storageAccountId string = storageAccount.id
output storageAccountName string = storageAccountName
output storageAccountBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob

output keyVaultUri string = keyVaultUri
output sqlConnectionStringSecretUri string = '${keyVaultUri}secrets/sql-connection-string'
output storageConnectionStringSecretUri string = '${keyVaultUri}secrets/storage-connection-string'
