@metadata({
  author: 'Cloud Architecture Team'
  version: '1.0.0'
  description: 'Azure OpenAI and AI Vision services for GenAI Recipe Extraction'
})

// ============================================================================
// PARAMETERS
// ============================================================================

@description('Azure region for OpenAI deployment (must support GPT-4o with Vision)')
@allowed([
  'canadacentral'
  'eastus2'
  'westus'
  'swedencentral'
  'australiaeast'
])
param openAILocation string = 'canadacentral'

@description('Azure region for AI Vision deployment')
param visionLocation string = 'canadacentral'

@description('Environment name (dev, prod)')
@allowed(['dev', 'prod'])
param environment string = 'prod'

@description('Base name for resources')
param baseName string = 'mealplan'

@description('Key Vault name for storing secrets')
param keyVaultName string

@description('Function App Principal ID for RBAC')
param functionAppPrincipalId string

@description('Deploy Azure AI Vision (optional enhancement)')
param deployVision bool = true

@description('GPT-4 Turbo model deployment capacity (tokens per minute in thousands)')
@minValue(1)
@maxValue(120)
param gpt4Capacity int = 10

// ============================================================================
// VARIABLES
// ============================================================================

var tags = {
  Environment: environment
  Project: 'MealPlanOrganizer'
  Feature: 'GenAI-RecipeExtraction'
  ManagedBy: 'Bicep'
}

var openAIAccountName = 'oai-${baseName}-${environment}-${uniqueString(resourceGroup().id)}'
var visionAccountName = 'cv-${baseName}-${environment}-${uniqueString(resourceGroup().id)}'
var openAICustomDomain = '${baseName}-openai-${environment}'
var visionCustomDomain = '${baseName}-vision-${environment}'

// Role Definition IDs
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

// ============================================================================
// AZURE OPENAI SERVICE
// ============================================================================

resource openAI 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAIAccountName
  location: openAILocation
  tags: tags
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  properties: {
    customSubDomainName: openAICustomDomain
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
    disableLocalAuth: false
  }
}

// GPT-4o with Vision deployment
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: gpt4Capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

// ============================================================================
// AZURE AI VISION (COMPUTER VISION)
// ============================================================================

resource vision 'Microsoft.CognitiveServices/accounts@2024-10-01' = if (deployVision) {
  name: visionAccountName
  location: visionLocation
  tags: tags
  sku: {
    name: 'S1'
  }
  kind: 'ComputerVision'
  properties: {
    customSubDomainName: visionCustomDomain
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
    disableLocalAuth: false
  }
}

// ============================================================================
// KEY VAULT SECRETS
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' existing = {
  name: keyVaultName
}

// OpenAI Endpoint Secret
resource openAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  parent: keyVault
  name: 'OpenAI-Endpoint'
  properties: {
    value: openAI.properties.endpoint
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// OpenAI API Key Secret
resource openAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  parent: keyVault
  name: 'OpenAI-ApiKey'
  properties: {
    value: openAI.listKeys().key1
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// OpenAI Deployment Name Secret
resource openAIDeploymentSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = {
  parent: keyVault
  name: 'OpenAI-DeploymentName'
  properties: {
    value: gpt4oDeployment.name
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Vision Endpoint Secret (conditional)
resource visionEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = if (deployVision) {
  parent: keyVault
  name: 'Vision-Endpoint'
  properties: {
    value: vision.properties.endpoint
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Vision API Key Secret (conditional)
resource visionKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = if (deployVision) {
  parent: keyVault
  name: 'Vision-ApiKey'
  properties: {
    value: vision.listKeys().key1
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// ============================================================================
// RBAC ROLE ASSIGNMENTS
// ============================================================================

// Grant Function App "Cognitive Services OpenAI User" role on OpenAI resource
resource functionOpenAIRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: openAI
  name: guid(openAI.id, functionAppPrincipalId, cognitiveServicesOpenAIUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Grant Function App "Cognitive Services User" role on Vision resource (conditional)
resource functionVisionRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployVision) {
  scope: vision
  name: guid(vision.id, functionAppPrincipalId, cognitiveServicesUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================================
// OUTPUTS
// ============================================================================

@description('Azure OpenAI Service resource ID')
output openAIId string = openAI.id

@description('Azure OpenAI Service name')
output openAIName string = openAI.name

@description('Azure OpenAI Service endpoint')
output openAIEndpoint string = openAI.properties.endpoint

@description('GPT-4o deployment name')
output gpt4DeploymentName string = gpt4oDeployment.name

@description('Azure AI Vision resource ID (empty if not deployed)')
output visionId string = deployVision ? vision.id : ''

@description('Azure AI Vision name (empty if not deployed)')
output visionName string = deployVision ? vision.name : ''

@description('Azure AI Vision endpoint (empty if not deployed)')
output visionEndpoint string = deployVision ? vision.properties.endpoint : ''

@description('Key Vault secret URI for OpenAI endpoint')
output openAIEndpointSecretUri string = openAIEndpointSecret.properties.secretUri

@description('Key Vault secret URI for OpenAI API key')
output openAIKeySecretUri string = openAIKeySecret.properties.secretUri

@description('Key Vault secret URI for OpenAI deployment name')
output openAIDeploymentSecretUri string = openAIDeploymentSecret.properties.secretUri

@description('Key Vault secret URI for Vision endpoint (empty if not deployed)')
output visionEndpointSecretUri string = deployVision ? visionEndpointSecret.properties.secretUri : ''

@description('Key Vault secret URI for Vision API key (empty if not deployed)')
output visionKeySecretUri string = deployVision ? visionKeySecret.properties.secretUri : ''