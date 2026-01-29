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

@description('SignalR Service name')
param signalRName string = 'signalr-mealplan-organizer'

@description('SignalR Service tier (Free_F1 or Standard_S1)')
@allowed([
  'Free_F1'
  'Standard_S1'
])
param signalRSku string = 'Free_F1'

@description('SignalR Service capacity (units)')
param signalRCapacity int = 1

@description('Log Analytics Workspace resource ID from Phase 1')
param logAnalyticsWorkspaceId string

@description('Virtual Network resource ID from Phase 1')
param vnetId string

// Note: Private endpoints are not supported in Free tier (Free_F1)
// For production with Standard tier (Standard_S1), uncomment the lines below:
// @description('Private Endpoint subnet resource ID from Phase 1')
// param privateEndpointSubnetId string
//
// @description('Private DNS Zone resource ID for SignalR (privatelink.service.signalr.net)')
// param signalRPrivateDnsZoneId string

@description('Key Vault resource ID from Phase 2')
param keyVaultId string

// ============================================================================
// VARIABLES
// ============================================================================

var tags = {
  Environment: environment
  Project: 'MealPlanOrganizer'
  Phase: '4-RealTime'
  ManagedBy: 'Bicep'
}

// ============================================================================
// SignalR Service
// ============================================================================

resource signalRService 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: signalRName
  location: location
  tags: tags
  sku: {
    name: signalRSku
    capacity: signalRCapacity
  }
  kind: 'SignalR'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'true'
      }
      {
        flag: 'EnableMessagingLogs'
        value: 'true'
      }
    ]
    // Note: Free tier does not support disabling public network access or advanced network ACLs
    // For production with Standard tier, uncomment the networkACLs and set publicNetworkAccess to 'Disabled'
    publicNetworkAccess: 'Enabled'
    tls: {
      clientCertEnabled: false
    }
  }
}

// ============================================================================
// Private Endpoint Support
// ============================================================================
// Note: Free tier (Free_F1) does not support private endpoints.
// Private endpoints are available only with Standard tier (Standard_S1) and above.
// To enable private endpoints, upgrade the SKU and uncomment the resource blocks below:
//
// resource signalRPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
//   name: '${signalRName}-pep-${uniqueString(signalRName)}'
//   location: location
//   tags: tags
//   properties: {
//     subnet: {
//       id: privateEndpointSubnetId
//     }
//     privateLinkServiceConnections: [
//       {
//         name: '${signalRName}-pep-connection'
//         properties: {
//           privateLinkServiceId: signalRService.id
//           groupIds: [
//             'signalr'
//           ]
//         }
//       }
//     ]
//   }
// }
//
// resource signalRPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
//   name: 'default'
//   parent: signalRPrivateEndpoint
//   properties: {
//     privateDnsZoneConfigs: [
//       {
//         name: 'privatelink-service-signalr-net'
//         properties: {
//           privateDnsZoneId: signalRPrivateDnsZoneId
//         }
//       }
//     ]
//   }
// }

// ============================================================================
// SignalR Service Diagnostic Settings
// ============================================================================

resource signalRDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${signalRName}-diag'
  scope: signalRService
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
// Key Vault Secret - SignalR Connection String
// ============================================================================

// Extract Key Vault name from resource ID for secret creation
var keyVaultName = last(split(keyVaultId, '/'))

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' existing = {
  name: keyVaultName
}

resource signalRConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  name: 'SignalRConnectionString'
  parent: keyVault
  properties: {
    value: 'Endpoint=https://${signalRService.properties.hostName};AccessKey=${signalRService.listKeys().primaryKey};Version=1.0;'
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
  tags: tags
}

resource signalRPrimaryKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  name: 'SignalRPrimaryKey'
  parent: keyVault
  properties: {
    value: signalRService.listKeys().primaryKey
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
  tags: tags
}

resource signalREndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  name: 'SignalREndpoint'
  parent: keyVault
  properties: {
    value: 'https://${signalRService.properties.hostName}'
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
  tags: tags
}

// ============================================================================
// OUTPUTS
// ============================================================================

@description('SignalR Service resource ID')
output signalRResourceId string = signalRService.id

@description('SignalR Service name')
output signalRName string = signalRService.name

@description('SignalR Service hostname')
output signalRHostname string = signalRService.properties.hostName

@description('SignalR Service endpoint URL')
output signalREndpoint string = 'https://${signalRService.properties.hostName}'
