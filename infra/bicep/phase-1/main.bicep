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

@description('Log Analytics Workspace name')
param logAnalyticsName string = 'log-mealplan-organizer'

@description('Application Insights name')
param appInsightsName string = 'appi-mealplan-organizer'

@description('Virtual Network name')
param vnetName string = 'vnet-mealplan-organizer'

@description('Virtual Network address space')
param vnetAddressSpace string = '10.0.0.0/16'

@description('Integration subnet name (for App Service/Function App VNet integration)')
param integrationSubnetName string = 'snet-integration'

@description('Integration subnet address prefix')
param integrationSubnetPrefix string = '10.0.2.0/24'

@description('Log Analytics retention period in days')
param logAnalyticsRetentionDays int = 30

@description('Tags for all resources')
param tags object = {
  Environment: environment
  Application: 'MealPlanOrganizer'
  ManagedBy: 'Bicep'
  CreatedDate: utcNow('u')
  CostCenter: 'Family'
}


// ============================================================================
// VARIABLES
// ============================================================================

var logAnalyticsSkuName = 'PerGB2018'
var appInsightsKind = 'web'

// ============================================================================
// RESOURCES: Log Analytics Workspace (TASK-002)
// ============================================================================

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: logAnalyticsSkuName
    }
    retentionInDays: logAnalyticsRetentionDays
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// ============================================================================
// RESOURCES: Application Insights (TASK-003)
// ============================================================================

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: appInsightsKind
  tags: tags
  properties: {
    Application_Type: appInsightsKind
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ============================================================================
// RESOURCES: Virtual Network with Subnets (TASK-004)
// ============================================================================

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressSpace
      ]
    }
    dhcpOptions: {
      dnsServers: []
    }
    subnets: [
      {
        name: integrationSubnetName
        properties: {
          addressPrefix: integrationSubnetPrefix
          privateEndpointNetworkPolicies: 'Enabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
          delegations: []
          serviceEndpoints: [
            {
              service: 'Microsoft.KeyVault'
            }
            {
              service: 'Microsoft.Sql'
            }
            {
              service: 'Microsoft.Storage'
            }
          ]
        }
      }
    ]
    enableDdosProtection: false
  }
}

// ============================================================================
// OUTPUTS
// ============================================================================

@description('Log Analytics Workspace ID')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('Log Analytics Workspace Name')
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name

@description('Application Insights ID')
output applicationInsightsId string = applicationInsights.id

@description('Application Insights Instrumentation Key')
output applicationInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey

@description('Virtual Network ID')
output virtualNetworkId string = virtualNetwork.id

@description('Virtual Network Name')
output virtualNetworkName string = virtualNetwork.name

@description('Integration Subnet ID')
output integrationSubnetId string = virtualNetwork.properties.subnets[0].id
