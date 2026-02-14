@metadata({
  author: 'Cloud Architecture Team'
  version: '1.0.0'
  description: 'Azure Notification Hub for push notifications to mobile devices'
})

// ============================================================================
// PARAMETERS
// ============================================================================

@description('Azure region for Notification Hub deployment')
param location string = resourceGroup().location

@description('Environment name (dev, prod)')
@allowed(['dev', 'prod'])
param environment string = 'prod'

@description('Base name for resources')
param baseName string = 'mealplan'

@description('Notification Hub namespace name (optional - auto-generated if not provided)')
param namespaceName string = ''

@description('Notification Hub name (optional - auto-generated if not provided)')
param hubName string = ''

@description('Notification Hub SKU (Free, Basic, or Standard)')
@allowed([
  'Free'
  'Basic'
  'Standard'
])
param sku string = 'Basic'

@description('Key Vault resource ID for storing connection strings')
param keyVaultId string

@description('Log Analytics Workspace resource ID for diagnostics')
param logAnalyticsWorkspaceId string = ''

// Platform-specific credentials (optional - configure after deployment or provide here)
@description('Apple Push Notification Service (APNS) certificate in Base64 (optional)')
@secure()
param apnsCertificate string = ''

@description('Apple Push Notification Service (APNS) certificate password (optional)')
@secure()
param apnsCertificatePassword string = ''

@description('APNS endpoint (Production or Sandbox)')
@allowed([
  'Production'
  'Sandbox'
])
param apnsEndpoint string = 'Production'

@description('Firebase Cloud Messaging (FCM) API key for Android (optional)')
@secure()
param fcmApiKey string = ''

@description('Windows Notification Service (WNS) Package SID (optional)')
param wnsPackageSid string = ''

@description('Windows Notification Service (WNS) Secret Key (optional)')
@secure()
param wnsSecretKey string = ''

// ============================================================================
// VARIABLES
// ============================================================================

var tags = {
  Environment: environment
  Project: 'MealPlanOrganizer'
  Feature: 'PushNotifications'
  ManagedBy: 'Bicep'
}

var uniqueSuffix = uniqueString(resourceGroup().id)
var actualNamespaceName = !empty(namespaceName) ? namespaceName : 'ntfns-${baseName}-${environment}-${uniqueSuffix}'
var actualHubName = !empty(hubName) ? hubName : 'ntf-${baseName}-${environment}'

// Extract Key Vault name from resource ID
var keyVaultName = last(split(keyVaultId, '/'))

// ============================================================================
// NOTIFICATION HUB NAMESPACE
// ============================================================================

resource notificationHubNamespace 'Microsoft.NotificationHubs/namespaces@2023-09-01' = {
  name: actualNamespaceName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    // Namespace-level settings
  }
}

// ============================================================================
// NOTIFICATION HUB
// ============================================================================

resource notificationHub 'Microsoft.NotificationHubs/namespaces/notificationHubs@2023-09-01' = {
  name: actualHubName
  parent: notificationHubNamespace
  location: location
  tags: tags
  properties: {
    // Apple Push Notification Service (APNS) - iOS
    apnsCredential: !empty(apnsCertificate) ? {
      properties: {
        apnsCertificate: apnsCertificate
        certificateKey: apnsCertificatePassword
        endpoint: 'gateway.${apnsEndpoint == 'Sandbox' ? 'sandbox.' : ''}push.apple.com'
      }
    } : null

    // Firebase Cloud Messaging (FCM) - Android
    // Note: FCM V1 API requires service account JSON, not just API key
    // For legacy FCM, use gcmCredential; for FCM V1, use fcmV1Credential
    gcmCredential: !empty(fcmApiKey) ? {
      properties: {
        googleApiKey: fcmApiKey
        gcmEndpoint: 'https://fcm.googleapis.com/fcm/send'
      }
    } : null

    // Windows Notification Service (WNS) - Windows/UWP
    wnsCredential: !empty(wnsPackageSid) && !empty(wnsSecretKey) ? {
      properties: {
        packageSid: wnsPackageSid
        secretKey: wnsSecretKey
        windowsLiveEndpoint: 'https://login.live.com/accesstoken.srf'
      }
    } : null
  }
}

// ============================================================================
// AUTHORIZATION RULES
// ============================================================================

// Default authorization rules are created automatically
// Get references to them for connection strings

resource defaultListenRule 'Microsoft.NotificationHubs/namespaces/notificationHubs/authorizationRules@2023-09-01' = {
  name: 'DefaultListenSharedAccessSignature'
  parent: notificationHub
  properties: {
    rights: [
      'Listen'
    ]
  }
}

resource defaultFullRule 'Microsoft.NotificationHubs/namespaces/notificationHubs/authorizationRules@2023-09-01' = {
  name: 'DefaultFullSharedAccessSignature'
  parent: notificationHub
  properties: {
    rights: [
      'Listen'
      'Manage'
      'Send'
    ]
  }
}

// ============================================================================
// DIAGNOSTIC SETTINGS
// ============================================================================

// Note: Notification Hub namespaces do not support metric export
resource namespaceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: '${actualNamespaceName}-diag'
  scope: notificationHubNamespace
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// KEY VAULT SECRETS
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' existing = {
  name: keyVaultName
}

resource notificationHubConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  name: 'NotificationHubConnectionString'
  parent: keyVault
  properties: {
    value: defaultFullRule.listKeys().primaryConnectionString
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
  tags: tags
}

resource notificationHubListenConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  name: 'NotificationHubListenConnectionString'
  parent: keyVault
  properties: {
    value: defaultListenRule.listKeys().primaryConnectionString
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
  tags: tags
}

resource notificationHubNameSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  name: 'NotificationHubName'
  parent: keyVault
  properties: {
    value: notificationHub.name
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

@description('Notification Hub Namespace resource ID')
output namespaceResourceId string = notificationHubNamespace.id

@description('Notification Hub Namespace name')
output namespaceName string = notificationHubNamespace.name

@description('Notification Hub resource ID')
output hubResourceId string = notificationHub.id

@description('Notification Hub name')
output hubName string = notificationHub.name

@description('Notification Hub endpoint')
output hubEndpoint string = 'sb://${notificationHubNamespace.name}.servicebus.windows.net/'
