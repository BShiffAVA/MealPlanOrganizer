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

@description('Key Vault name')
param keyVaultName string = 'kv-mealplan-org'

@description('Enable purge protection on Key Vault')
param enablePurgeProtection bool = true

@description('Enable RBAC authorization on Key Vault')
param enableRbacAuthorization bool = true

@description('Tags for all resources')
param tags object = {
  Environment: environment
  Application: 'MealPlanOrganizer'
  ManagedBy: 'Bicep'
  CostCenter: 'Family'
  Region: location
}

@description('Phase 1 - Log Analytics Workspace name')
param logAnalyticsWorkspaceName string = 'log-mealplan-organizer'

// ============================================================================
// VARIABLES
// ============================================================================

var subscriptionId = subscription().subscriptionId
var resourceGroupName = resourceGroup().name
var keyVaultSkuName = 'standard'
var diagnosticSettingsName = '${keyVaultName}-diag'
var logAnalyticsWorkspaceId = '/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}/providers/Microsoft.OperationalInsights/workspaces/${logAnalyticsWorkspaceName}'

// ============================================================================
// RESOURCES: Key Vault (TASK-006)
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: keyVaultSkuName
    }
    accessPolicies: []
    enableRbacAuthorization: enableRbacAuthorization
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: enablePurgeProtection
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// ============================================================================
// RESOURCES: Diagnostic Settings (TASK-008)
// ============================================================================

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: diagnosticSettingsName
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AuditEvent'
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
// OUTPUTS
// ============================================================================

@description('Key Vault ID')
output keyVaultId string = keyVault.id

@description('Key Vault Name')
output keyVaultName string = keyVault.name

@description('Key Vault URI')
output keyVaultUri string = keyVault.properties.vaultUri
