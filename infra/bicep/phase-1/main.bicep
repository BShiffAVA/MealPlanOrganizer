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

@description('Private Endpoints subnet name')
param privateEndpointSubnetName string = 'snet-private-endpoints'

@description('Private Endpoints subnet address prefix')
param privateEndpointSubnetPrefix string = '10.0.1.0/24'

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

@description('VNet link name for SQL Database Private DNS Zone')
param vnetLinkNameSql string = 'sql-link'

@description('VNet link name for Key Vault Private DNS Zone')
param vnetLinkNameKeyVault string = 'keyvault-link'

@description('VNet link name for Blob Storage Private DNS Zone')
param vnetLinkNameBlob string = 'blob-link'

@description('VNet link name for SignalR Private DNS Zone')
param vnetLinkNameSignalR string = 'signalr-link'

// ============================================================================
// VARIABLES
// ============================================================================

var logAnalyticsSkuName = 'PerGB2018'
var appInsightsKind = 'web'
// @allowed([ 'privatelink.database.windows.net', 'privatelink.blob.core.windows.net' ])
var privateDnsZones = [
  'privatelink.database.windows.net'      // SQL Database - hardcoded for Azure public cloud
  'privatelink.vaultcore.azure.net'       // Key Vault
  'privatelink.blob.core.windows.net'     // Storage Account - hardcoded for Azure public cloud
  'privatelink.signalr.net'               // SignalR Service
]

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
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
          delegations: []
          serviceEndpoints: []
        }
      }
    ]
    enableDdosProtection: false
  }
}

// ============================================================================
// RESOURCES: Private DNS Zones (TASK-005)
// ============================================================================

resource privateDnsZonesSql 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZones[0]
  location: 'global'
  tags: tags
  properties: {}
}

resource privateDnsZonesKeyVault 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZones[1]
  location: 'global'
  tags: tags
  properties: {}
}

resource privateDnsZonesBlob 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZones[2]
  location: 'global'
  tags: tags
  properties: {}
}

resource privateDnsZonesSignalR 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZones[3]
  location: 'global'
  tags: tags
  properties: {}
}

// ============================================================================
// RESOURCES: VNet Links for Private DNS Zones
// ============================================================================

resource privateDnsZoneVNetLinkSql 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZonesSql
  name: vnetLinkNameSql
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource privateDnsZoneVNetLinkKeyVault 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZonesKeyVault
  name: vnetLinkNameKeyVault
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource privateDnsZoneVNetLinkBlob 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZonesBlob
  name: vnetLinkNameBlob
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource privateDnsZoneVNetLinkSignalR 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZonesSignalR
  name: vnetLinkNameSignalR
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
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

@description('Private Endpoints Subnet ID')
output privateEndpointSubnetId string = virtualNetwork.properties.subnets[1].id

@description('Private DNS Zone SQL Database ID')
output privateDnsZoneSqlId string = privateDnsZonesSql.id

@description('Private DNS Zone Key Vault ID')
output privateDnsZonesKeyVaultId string = privateDnsZonesKeyVault.id

@description('Private DNS Zone Blob Storage ID')
output privateDnsZonesBlobId string = privateDnsZonesBlob.id

@description('Private DNS Zone SignalR ID')
output privateDnsZonesSignalRId string = privateDnsZonesSignalR.id
