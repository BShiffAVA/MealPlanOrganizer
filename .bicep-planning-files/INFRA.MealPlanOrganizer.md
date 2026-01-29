---
goal: Deploy Azure infrastructure for Meal Plan Organizer mobile application
---

# Introduction

This implementation plan defines the complete Azure infrastructure deployment for the Meal Plan Organizer application - a family-focused mobile application built with .NET MAUI that enables household members to collaborate on rating recipes, creating meal plans, and discovering new recipes. The infrastructure is designed to support 2 adults and 2 teens with a strict monthly budget constraint of under $50, utilizing Azure PaaS services with consumption-based pricing models and Basic tier configurations where appropriate.

## Resources

### resourceGroup

```yaml
name: rg-mealplan-organizer
kind: Raw
type: Microsoft.Resources/resourceGroups@2024-03-01

purpose: Container for all Meal Plan Organizer infrastructure resources
dependsOn: []

parameters:
  required:
    - name: location
      type: string
      description: Azure region for deployment
      example: eastus2
    - name: tags
      type: object
      description: Resource tags for organization and cost tracking
      example: { Environment: 'Production', Application: 'MealPlanOrganizer', CostCenter: 'Family' }

outputs:
  - name: resourceGroupName
    type: string
    description: Name of the created resource group
  - name: resourceGroupId
    type: string
    description: Resource ID of the resource group

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/manage-resource-groups-portal
```

### logAnalyticsWorkspace

```yaml
name: log-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/operational-insights/workspace:0.11.0
type: Microsoft.OperationalInsights/workspaces@2025-02-01

purpose: Centralized logging and monitoring for all application components
dependsOn: [resourceGroup]

parameters:
  required:
    - name: name
      type: string
      description: Workspace name
      example: log-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: sku
      type: string
      description: Pricing tier
      example: PerGB2018
    - name: retentionInDays
      type: int
      description: Log retention period to minimize costs
      example: 30
  optional:
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: workspaceId
    type: string
    description: Resource ID of the workspace
  - name: workspaceResourceId
    type: string
    description: Full resource ID for diagnostic settings

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/log-analytics-workspace-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/operational-insights/workspace
```

### applicationInsights

```yaml
name: appi-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/insights/component:0.5.0
type: Microsoft.Insights/components@2020-02-02

purpose: Application performance monitoring, telemetry collection, and diagnostics
dependsOn: [logAnalyticsWorkspace]

parameters:
  required:
    - name: name
      type: string
      description: Application Insights instance name
      example: appi-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: workspaceResourceId
      type: string
      description: Log Analytics workspace for data storage
      example: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer
    - name: applicationType
      type: string
      description: Application type
      example: web
  optional:
    - name: retentionInDays
      type: int
      description: Data retention period
      default: 90
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: instrumentationKey
    type: string
    description: Instrumentation key for SDK configuration
  - name: connectionString
    type: string
    description: Connection string for modern SDKs
  - name: resourceId
    type: string
    description: Resource ID

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/insights/component
```

### virtualNetwork

```yaml
name: vnet-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/network/virtual-network:0.7.0
type: Microsoft.Network/virtualNetworks@2024-05-01

purpose: Network isolation and private connectivity for all Azure resources
dependsOn: [resourceGroup]

parameters:
  required:
    - name: name
      type: string
      description: Virtual network name
      example: vnet-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: addressPrefixes
      type: array
      description: VNet address space
      example: ['10.0.0.0/16']
    - name: subnets
      type: array
      description: Subnet configurations
      example: 
        - name: snet-private-endpoints
          addressPrefix: 10.0.1.0/24
          privateEndpointNetworkPolicies: Disabled
        - name: snet-integration
          addressPrefix: 10.0.2.0/24
          delegations: 
            - name: delegation
              properties:
                serviceName: Microsoft.Web/serverFarms
  optional:
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: VNet resource ID
  - name: subnetResourceIds
    type: object
    description: Map of subnet names to resource IDs
  - name: name
    type: string
    description: VNet name

references:
  docs: https://learn.microsoft.com/en-us/azure/virtual-network/virtual-networks-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/network/virtual-network
```

### privateDnsZones

```yaml
name: pdnsz-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/network/private-dns-zone:0.8.0
type: Microsoft.Network/privateDnsZones@2024-06-01

purpose: DNS resolution for private endpoints (SQL, Key Vault, Storage, SignalR)
dependsOn: [virtualNetwork]

parameters:
  required:
    - name: name
      type: string
      description: Private DNS zone name
      example: privatelink.database.windows.net
    - name: virtualNetworkLinks
      type: array
      description: VNet link configurations
      example:
        - virtualNetworkResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer
          registrationEnabled: false
  optional:
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Private DNS zone resource ID
  - name: name
    type: string
    description: Private DNS zone name

references:
  docs: https://learn.microsoft.com/en-us/azure/dns/private-dns-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/network/private-dns-zone

notes: |
  Multiple zones required:
  - privatelink.database.windows.net (SQL)
  - privatelink.vaultcore.azure.net (Key Vault)
  - privatelink.blob.core.windows.net (Storage - Blob)
  - privatelink.signalr.net (SignalR)
```

### keyVault

```yaml
name: kv-mealplan-org
kind: AVM
avmModule: br/public:avm/res/key-vault/vault:0.12.0
type: Microsoft.KeyVault/vaults@2024-11-01

purpose: Secure storage of connection strings, secrets, and application configuration
dependsOn: [virtualNetwork, privateDnsZones, logAnalyticsWorkspace]

parameters:
  required:
    - name: name
      type: string
      description: Key Vault name (globally unique, max 24 chars)
      example: kv-mealplan-org
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: sku
      type: string
      description: SKU tier
      example: standard
    - name: enableRbacAuthorization
      type: bool
      description: Use RBAC instead of access policies
      example: true
    - name: enablePurgeProtection
      type: bool
      description: Enable purge protection
      example: true
    - name: softDeleteRetentionInDays
      type: int
      description: Soft delete retention period
      example: 7
    - name: networkAcls
      type: object
      description: Network access rules
      example:
        bypass: AzureServices
        defaultAction: Deny
  optional:
    - name: privateEndpoints
      type: array
      description: Private endpoint configurations
      default:
        - name: pe-keyvault
          subnetResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer/subnets/snet-private-endpoints
          privateDnsZoneGroup:
            privateDnsZoneGroupConfigs:
              - privateDnsZoneResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/privateDnsZones/privatelink.vaultcore.azure.net
    - name: diagnosticSettings
      type: array
      description: Diagnostic settings configuration
      default:
        - workspaceResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer
          logCategoriesAndGroups:
            - categoryGroup: allLogs
          metricCategories:
            - category: AllMetrics
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Key Vault resource ID
  - name: uri
    type: string
    description: Key Vault URI
  - name: name
    type: string
    description: Key Vault name

references:
  docs: https://learn.microsoft.com/en-us/azure/key-vault/general/overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/key-vault/vault
```

### sqlServer

```yaml
name: sql-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/sql/server:0.11.0
type: Microsoft.Sql/servers@2023-08-01-preview

purpose: Managed SQL database hosting for application data (users, recipes, meal plans, ratings)
dependsOn: [virtualNetwork, privateDnsZones, logAnalyticsWorkspace, keyVault]

parameters:
  required:
    - name: name
      type: string
      description: SQL Server name (globally unique)
      example: sql-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: administratorLogin
      type: string
      description: Admin username
      example: sqladmin
    - name: administratorLoginPassword
      type: securestring
      description: Admin password (store in Key Vault)
      example: (reference Key Vault secret)
    - name: minimalTlsVersion
      type: string
      description: Minimum TLS version
      example: '1.2'
    - name: publicNetworkAccess
      type: string
      description: Public network access
      example: Disabled
  optional:
    - name: administrators
      type: object
      description: Azure AD admin configuration
      default:
        azureADOnlyAuthentication: true
        login: MealPlanOrgAdmins
        principalType: Group
        sid: (Azure AD Group Object ID)
    - name: privateEndpoints
      type: array
      description: Private endpoint for SQL connectivity
      default:
        - name: pe-sqlserver
          subnetResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer/subnets/snet-private-endpoints
          privateDnsZoneGroup:
            privateDnsZoneGroupConfigs:
              - privateDnsZoneResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/privateDnsZones/privatelink.database.windows.net
    - name: databases
      type: array
      description: Database configurations
      default:
        - name: MealPlanOrganizerDB
          skuName: Basic
          skuTier: Basic
          maxSizeBytes: 2147483648
          zoneRedundant: false
          collation: SQL_Latin1_General_CP1_CI_AS
          autoPauseDelay: null
          diagnosticSettings:
            - workspaceResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer
              logCategoriesAndGroups:
                - categoryGroup: allLogs
              metricCategories:
                - category: AllMetrics
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: SQL Server resource ID
  - name: fullyQualifiedDomainName
    type: string
    description: SQL Server FQDN
  - name: databases
    type: array
    description: Deployed databases

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-sql/database/sql-database-paas-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/sql/server
```

### storageAccount

```yaml
name: stmealplanorg
kind: AVM
avmModule: br/public:avm/res/storage/storage-account:0.17.1
type: Microsoft.Storage/storageAccounts@2024-01-01

purpose: Blob storage for recipe images uploaded by family members
dependsOn: [virtualNetwork, privateDnsZones, logAnalyticsWorkspace]

parameters:
  required:
    - name: name
      type: string
      description: Storage account name (globally unique, 3-24 lowercase alphanumeric)
      example: stmealplanorg
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: skuName
      type: string
      description: SKU for cost optimization
      example: Standard_LRS
    - name: kind
      type: string
      description: Storage account kind
      example: StorageV2
    - name: accessTier
      type: string
      description: Access tier
      example: Hot
    - name: allowBlobPublicAccess
      type: bool
      description: Disable public blob access
      example: false
    - name: publicNetworkAccess
      type: string
      description: Public network access
      example: Disabled
    - name: networkAcls
      type: object
      description: Network ACLs
      example:
        bypass: AzureServices
        defaultAction: Deny
  optional:
    - name: blobServices
      type: object
      description: Blob service configuration
      default:
        deleteRetentionPolicyEnabled: true
        deleteRetentionPolicyDays: 7
        containerDeleteRetentionPolicyEnabled: true
        containerDeleteRetentionPolicyDays: 7
        containers:
          - name: recipe-images
            publicAccess: None
    - name: privateEndpoints
      type: array
      description: Private endpoints for blob storage
      default:
        - name: pe-storage-blob
          service: blob
          subnetResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer/subnets/snet-private-endpoints
          privateDnsZoneGroup:
            privateDnsZoneGroupConfigs:
              - privateDnsZoneResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net
    - name: diagnosticSettings
      type: array
      description: Diagnostic settings
      default:
        - workspaceResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer
          metricCategories:
            - category: AllMetrics
    - name: managementPolicyRules
      type: array
      description: Lifecycle management rules
      default:
        - name: DeleteOldImages
          type: Lifecycle
          definition:
            actions:
              baseBlob:
                delete:
                  daysAfterModificationGreaterThan: 365
            filters:
              blobTypes:
                - blockBlob
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Storage account resource ID
  - name: primaryBlobEndpoint
    type: string
    description: Primary blob endpoint
  - name: name
    type: string
    description: Storage account name

references:
  docs: https://learn.microsoft.com/en-us/azure/storage/common/storage-account-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/storage/storage-account
```

### signalRService

```yaml
name: signalr-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/signal-r-service/signal-r:0.6.0
type: Microsoft.SignalRService/signalR@2024-03-01

purpose: Real-time notifications for recipe ratings and meal plan updates
dependsOn: [virtualNetwork, privateDnsZones, logAnalyticsWorkspace]

parameters:
  required:
    - name: name
      type: string
      description: SignalR Service name
      example: signalr-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: sku
      type: object
      description: SKU configuration for serverless mode
      example:
        name: Free_F1
        tier: Free
        capacity: 1
    - name: features
      type: array
      description: Service features
      example:
        - flag: ServiceMode
          value: Serverless
    - name: publicNetworkAccess
      type: string
      description: Public network access
      example: Disabled
    - name: networkAcls
      type: object
      description: Network ACLs
      example:
        defaultAction: Deny
        publicNetwork:
          allow:
            - ServerConnection
  optional:
    - name: privateEndpoints
      type: array
      description: Private endpoint configuration
      default:
        - name: pe-signalr
          subnetResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer/subnets/snet-private-endpoints
          privateDnsZoneGroup:
            privateDnsZoneGroupConfigs:
              - privateDnsZoneResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/privateDnsZones/privatelink.signalr.net
    - name: diagnosticSettings
      type: array
      description: Diagnostic settings
      default:
        - workspaceResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer
          logCategoriesAndGroups:
            - categoryGroup: allLogs
          metricCategories:
            - category: AllMetrics
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: SignalR Service resource ID
  - name: hostName
    type: string
    description: Service host name
  - name: externalIP
    type: string
    description: Service external IP

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-signalr/signalr-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/signal-r-service/signal-r

notes: |
  ORPHANED MODULE WARNING: SignalR AVM module shows as orphaned.
  Consider migration to Free_F1 tier initially, upgrade to Standard if needed.
  Monitor costs carefully and implement household-scoped SignalR groups.
```

### appServicePlan

```yaml
name: asp-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/web/serverfarm:0.4.0
type: Microsoft.Web/serverfarms@2024-11-01

purpose: Hosting plan for Azure Functions (consumption-based for cost optimization)
dependsOn: [resourceGroup]

parameters:
  required:
    - name: name
      type: string
      description: App Service Plan name
      example: asp-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: sku
      type: object
      description: SKU for consumption plan
      example:
        name: Y1
        tier: Dynamic
    - name: kind
      type: string
      description: Plan kind
      example: functionapp
    - name: reserved
      type: bool
      description: Reserved for Linux
      example: false
  optional:
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: App Service Plan resource ID
  - name: name
    type: string
    description: Plan name

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-functions/consumption-plan
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/serverfarm
```

### functionApp

```yaml
name: func-mealplan-organizer
kind: AVM
avmModule: br/public:avm/res/web/site:0.15.0
type: Microsoft.Web/sites@2024-11-01

purpose: Serverless API backend for mobile application
dependsOn: [appServicePlan, storageAccount, applicationInsights, sqlServer, keyVault, signalRService, virtualNetwork]

parameters:
  required:
    - name: name
      type: string
      description: Function App name
      example: func-mealplan-organizer
    - name: location
      type: string
      description: Azure region
      example: eastus2
    - name: kind
      type: string
      description: App kind
      example: functionapp
    - name: serverFarmResourceId
      type: string
      description: App Service Plan resource ID
      example: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Web/serverfarms/asp-mealplan-organizer
    - name: managedIdentities
      type: object
      description: Managed identity configuration
      example:
        systemAssigned: true
    - name: siteConfig
      type: object
      description: Site configuration
      example:
        netFrameworkVersion: v8.0
        ftpsState: Disabled
        minTlsVersion: '1.2'
        scmMinTlsVersion: '1.2'
        use32BitWorkerProcess: false
        cors:
          allowedOrigins:
            - '*'
        appSettings:
          - name: FUNCTIONS_EXTENSION_VERSION
            value: ~4
          - name: FUNCTIONS_WORKER_RUNTIME
            value: dotnet-isolated
          - name: APPINSIGHTS_INSTRUMENTATIONKEY
            value: (reference Application Insights)
          - name: APPLICATIONINSIGHTS_CONNECTION_STRING
            value: (reference Application Insights)
          - name: AzureWebJobsStorage
            value: (reference Storage Account connection string)
          - name: AzureSignalRConnectionString
            value: '@Microsoft.KeyVault(SecretUri=https://kv-mealplan-org.vault.azure.net/secrets/SignalRConnectionString/)'
          - name: SqlConnectionString
            value: '@Microsoft.KeyVault(SecretUri=https://kv-mealplan-org.vault.azure.net/secrets/SqlConnectionString/)'
          - name: StorageConnectionString
            value: '@Microsoft.KeyVault(SecretUri=https://kv-mealplan-org.vault.azure.net/secrets/StorageConnectionString/)'
    - name: virtualNetworkSubnetId
      type: string
      description: Subnet for VNet integration
      example: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer/subnets/snet-integration
  optional:
    - name: httpsOnly
      type: bool
      description: HTTPS only
      default: true
    - name: diagnosticSettings
      type: array
      description: Diagnostic settings
      default:
        - workspaceResourceId: /subscriptions/{subscriptionId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer
          logCategoriesAndGroups:
            - categoryGroup: allLogs
          metricCategories:
            - category: AllMetrics
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Function App resource ID
  - name: defaultHostName
    type: string
    description: Default hostname
  - name: systemAssignedMIPrincipalId
    type: string
    description: System-assigned managed identity principal ID

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-functions/functions-overview
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/site
```

### entraExternalIDTenant

```yaml
name: mealplanorg-external
kind: Raw
type: Microsoft.Entra/externalTenant@2024-01-01

purpose: Customer identity and access management (CIAM) for mobile app users with modern sign-in experiences
dependsOn: [resourceGroup]

parameters:
  required:
    - name: tenantName
      type: string
      description: External tenant display name
      example: MealPlanOrganizer
    - name: tenantType
      type: string
      description: Tenant configuration type
      example: external
  optional:
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: tenantId
    type: string
    description: External tenant ID
  - name: tenantDomain
    type: string
    description: External tenant domain (e.g., mealplanorg.onmicrosoft.com)

references:
  docs: https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam
  pricing: https://aka.ms/ExternalIDPricing

notes: |
  Microsoft Entra External ID (in external tenant configuration):
  - Next-generation CIAM solution replacing deprecated Azure AD B2C
  - First 50,000 MAU free (4 users = always free)
  - Features: User flows, social sign-in, custom branding, MFA, Conditional Access
  - Native MSAL support for .NET MAUI mobile apps (iOS/Android)
  - Fully integrated with Microsoft Entra security ecosystem
  
  Configuration (Manual Portal Steps):
  1. Create external tenant in Microsoft Entra admin center
  2. Link external tenant to Azure subscription for billing
  3. Create user flows (sign-up/sign-in)
  4. Configure sign-in methods (email/password, social providers)
  5. Register mobile application (OIDC)
  6. Customize company branding for sign-in pages
  7. Enable MFA in Conditional Access policies if desired
```

# Implementation Plan

The deployment follows a phased approach that establishes foundational networking and security infrastructure first, then layers on data storage and compute resources, and finally configures application-specific services. Each phase is designed to be independently deployable and testable.

## Phase 1 — Foundation & Networking

**Objective:** Establish resource group, networking foundation, monitoring infrastructure, and DNS resolution for private connectivity.

This phase creates the fundamental Azure infrastructure components required by all subsequent resources. The virtual network establishes network isolation boundaries, while Log Analytics and Application Insights provide centralized observability. Private DNS zones enable secure name resolution for private endpoints.

- IMPLEMENT-GOAL-001: Deploy foundational infrastructure and networking components

| Task | Description | Action |
| -------- | --------------------------------- | -------------------------------------- |
| TASK-001 | Create resource group | Deploy Microsoft.Resources/resourceGroups |
| TASK-002 | Deploy Log Analytics Workspace with 30-day retention | Deploy AVM module avm/res/operational-insights/workspace |
| TASK-003 | Deploy Application Insights linked to Log Analytics | Deploy AVM module avm/res/insights/component |
| TASK-004 | Create VNet with subnets for private endpoints and integration | Deploy AVM module avm/res/network/virtual-network with 2 subnets |
| TASK-005 | Deploy private DNS zones for SQL, Key Vault, Storage, SignalR | Deploy 4 instances of AVM module avm/res/network/private-dns-zone with VNet links |

## Phase 2 — Security & Secrets Management

**Objective:** Deploy Key Vault with private endpoint for secure storage of connection strings, credentials, and application secrets with RBAC-based access control.

Key Vault serves as the central secrets management solution. All sensitive configuration values (SQL connection strings, storage keys, SignalR connection strings) will be stored here and referenced by the Function App using Key Vault references with managed identity authentication.

- IMPLEMENT-GOAL-002: Deploy secure secrets management infrastructure

| Task | Description | Action |
| -------- | --------------------------------- | -------------------------------------- |
| TASK-006 | Deploy Key Vault with RBAC authorization and network isolation | Deploy AVM module avm/res/key-vault/vault with private endpoint |
| TASK-007 | Configure private endpoint for Key Vault in private endpoint subnet | Configure privateEndpoints parameter with DNS zone group |
| TASK-008 | Enable diagnostic settings to Log Analytics | Configure diagnosticSettings parameter |
| TASK-009 | Verify Key Vault accessibility from private endpoint | Test connectivity and DNS resolution |

## Phase 3 — Data Tier

**Objective:** Deploy SQL Database (Basic tier) and Storage Account with private endpoints for recipe images, ensuring data isolation and cost-optimized configuration.

The data tier provides persistent storage for structured application data (SQL) and unstructured binary data (Blob Storage for recipe images). Both services are configured with Basic/Standard tiers to minimize costs while meeting application requirements.

- IMPLEMENT-GOAL-003: Deploy data storage infrastructure with private connectivity

| Task | Description | Action |
| -------- | --------------------------------- | -------------------------------------- |
| TASK-010 | Deploy SQL Server with Azure AD authentication and private endpoint | Deploy AVM module avm/res/sql/server with privateEndpoints configuration |
| TASK-011 | Create MealPlanOrganizerDB database (Basic tier, 2GB) | Configure databases parameter in SQL Server module |
| TASK-012 | Store SQL connection string in Key Vault as secret | Create Key Vault secret SqlConnectionString |
| TASK-013 | Deploy Storage Account (Standard_LRS) with blob private endpoint | Deploy AVM module avm/res/storage/storage-account |
| TASK-014 | Create recipe-images blob container with private access | Configure blobServices.containers parameter |
| TASK-015 | Configure blob lifecycle management (delete after 365 days) | Configure managementPolicyRules parameter |
| TASK-016 | Store Storage connection string in Key Vault | Create Key Vault secret StorageConnectionString |
| TASK-017 | Enable diagnostic settings for SQL and Storage to Log Analytics | Configure diagnosticSettings on both resources |

## Phase 4 — Real-Time Communication

**Objective:** Deploy Azure SignalR Service in serverless mode with private endpoint for real-time notifications of recipe ratings and meal plan changes.

SignalR Service enables push notifications to mobile clients when family members update recipes or meal plans. Serverless mode is cost-effective for low-concurrency scenarios. The Free tier supports up to 20 concurrent connections, sufficient for a 4-person household.

- IMPLEMENT-GOAL-004: Deploy real-time messaging infrastructure

| Task | Description | Action |
| -------- | --------------------------------- | -------------------------------------- |
| TASK-018 | Deploy SignalR Service (Free tier, Serverless mode) with private endpoint | Deploy AVM module avm/res/signal-r-service/signal-r |
| TASK-019 | Configure service mode to Serverless | Set features array with ServiceMode flag |
| TASK-020 | Store SignalR connection string in Key Vault | Create Key Vault secret SignalRConnectionString |
| TASK-021 | Enable diagnostic settings to Log Analytics | Configure diagnosticSettings parameter |
| TASK-022 | Configure network isolation with private endpoint | Configure privateEndpoints parameter |

## Phase 5 — Compute & Application Tier

**Objective:** Deploy Azure Functions on Consumption Plan with VNet integration, managed identity for Key Vault access, and HTTPS-only configuration for API backend.

The Function App hosts the REST API backend that serves the mobile application. Consumption Plan provides automatic scaling and pay-per-execution pricing. VNet integration enables secure connectivity to private endpoints. System-assigned managed identity provides password-less authentication to Key Vault, SQL, Storage, and SignalR.

- IMPLEMENT-GOAL-005: Deploy serverless compute infrastructure

| Task | Description | Action |
| -------- | --------------------------------- | -------------------------------------- |
| TASK-023 | Deploy App Service Plan (Consumption/Y1 tier) | Deploy AVM module avm/res/web/serverfarm |
| TASK-024 | Deploy Function App with system-assigned managed identity | Deploy AVM module avm/res/web/site |
| TASK-025 | Configure VNet integration with integration subnet | Set virtualNetworkSubnetId parameter |
| TASK-026 | Configure application settings with Key Vault references | Set siteConfig.appSettings with @Microsoft.KeyVault references |
| TASK-027 | Grant Function App managed identity Key Vault Secrets User role | Assign RBAC role on Key Vault |
| TASK-028 | Grant Function App managed identity SQL DB Contributor role | Assign RBAC role on SQL Database |
| TASK-029 | Grant Function App managed identity Storage Blob Data Contributor | Assign RBAC role on Storage Account |
| TASK-030 | Grant Function App managed identity SignalR Service Owner role | Assign RBAC role on SignalR Service |
| TASK-031 | Enable diagnostic settings to Log Analytics | Configure diagnosticSettings parameter |
| TASK-032 | Verify Function App can resolve private endpoints | Test DNS resolution and connectivity |

## Phase 6 — Identity & Authentication

**Objective:** Configure Microsoft Entra External ID (in external tenant) for mobile application user authentication with email/password and social identity providers.

**NOTE:** Azure AD B2C is no longer available for new customers as of May 1, 2025. Microsoft Entra External ID is the recommended CIAM solution with all new features and better integration with the Microsoft Entra platform.

- IMPLEMENT-GOAL-006: Configure customer identity and access management

| Task | Description | Action |
| -------- | --------------------------------- | -------------------------------------- |
| TASK-033 | Create Microsoft Entra External ID tenant via Portal (Manual) | Navigate to Microsoft Entra admin center; create new external tenant |
| TASK-034 | Link external tenant to Azure subscription | Enable billing and feature access |
| TASK-035 | Configure sign-up/sign-in user flow | Create user flow with email/password and optional social providers |
| TASK-036 | Register .NET MAUI mobile application | Create app registration; configure native app redirect URIs for iOS/Android |
| TASK-037 | Configure identity providers (email, Google, Facebook, Apple) | Enable desired social identity providers in tenant |
| TASK-038 | Set up company branding and customization | Customize sign-in page appearance, language support |
| TASK-039 | Enable MFA in Conditional Access (Optional) | Configure email OTP or SMS second factor for security |
| TASK-040 | Store External ID configuration in Key Vault | Create secrets: ExternalIdTenantId, ExternalIdClientId, ExternalIdClientSecret |
| TASK-041 | Update Function App app settings with External ID config | Add environment variables for tenant discovery and token validation |
| TASK-042 | Verify OIDC token validation in Function App middleware | Test JWT validation from External ID issuer |
| TASK-043 | Test sign-in flow with mobile app | Verify MSAL integration with .NET MAUI app on iOS/Android |

## High-level design

### Network Architecture

```
Internet
    |
    | (HTTPS only)
    |
[Microsoft Entra External ID] ←--(OIDC)--→ [.NET MAUI Mobile Apps]
    |                               |
    |                               | (HTTPS REST API)
    |                               ↓
    |                     [Azure Functions (Consumption)]
    |                               |
    |                               | VNet Integration
    |                               ↓
    |                     [VNet: 10.0.0.0/16]
    |                               |
    |              +----------------+----------------+
    |              |                                 |
    |    [Subnet: Integration]        [Subnet: Private Endpoints]
    |      10.0.2.0/24                    10.0.1.0/24
    |              |                                 |
    |              |         +-------+-------+-------+-------+
    |              |         |       |       |       |       |
    |              |    [PE: SQL] [PE: KV] [PE: Blob] [PE: SignalR]
    |              |         |       |       |       |
    |              ↓         ↓       ↓       ↓       ↓
    |      [Private DNS Zones]
    |              |
    |     +--------+--------+--------+--------+
    |     |        |        |        |        |
    |  [SQL DB] [Key Vault] [Storage] [SignalR]
    |     |        |        |        |
    |     |        ↓        |        |
    |     |   [Secrets]     |        |
    |     |                 |        |
    |     +-----------------+--------+
    |                 |
    |                 ↓
    |         [App Insights]
    |                 |
    |                 ↓
    |       [Log Analytics Workspace]
```

### Data Flow for Recipe Rating Update

1. User opens .NET MAUI mobile app → Authenticates via Microsoft Entra External ID → Receives JWT token
2. User rates recipe (1-5 stars) → Mobile app sends HTTPS POST to Function App API
3. Function App validates JWT → Uses managed identity to access Key Vault → Retrieves SQL connection string
4. Function App writes rating to SQL Database via private endpoint
5. Function App calculates new average rating
6. Function App uses managed identity → Retrieves SignalR connection string from Key Vault
7. Function App sends SignalR message to household group with updated rating
8. All family member devices receive real-time push notification
9. Mobile apps refresh recipe detail view with new average rating

### Cost Optimization Strategy

- **App Service Plan:** Consumption (Y1) - Pay per execution, first 1 million executions free
- **SQL Database:** Basic tier (2GB) - $5/month
- **Storage Account:** Standard_LRS - ~$0.02/GB/month + transaction costs
- **SignalR Service:** Free tier - 20 concurrent connections, 20,000 messages/day
- **Application Insights:** First 5GB/month free, 90-day retention
- **Log Analytics:** 30-day retention to minimize costs
- **Key Vault:** Standard tier - $0.03/10,000 operations
- **Virtual Network:** No charge
- **Private DNS Zones:** $0.50/zone/month × 4 = $2/month
- **Private Endpoints:** $0.01/hour × 4 = $2.88/month

**Estimated Monthly Cost:** ~$10-15/month (well under $50 budget)

### Security Controls

- **Network Isolation:** All PaaS services accessible only via private endpoints
- **Secrets Management:** All connection strings and credentials stored in Key Vault
- **Authentication:** Microsoft Entra External ID with OIDC/OAuth2
- **Authorization:** Function App uses managed identity (no stored credentials)
- **Transport:** HTTPS/TLS 1.2+ enforced on all endpoints
- **Data At Rest:** SQL TDE enabled, Storage encryption enabled by default
- **Monitoring:** All services send logs to Log Analytics for security audit

### Monitoring & Alerting

- **Application Insights:** Tracks Function App performance, exceptions, dependencies
- **Log Analytics:** Centralized log aggregation from all services
- **Recommended Alerts:**
  - Function App error rate > 5%
  - SQL Database DTU > 80%
  - Storage Account throttling errors
  - SignalR Service connection errors
  - Key Vault access failures

### Deployment Sequence

1. **Infrastructure (Phases 1-2):** Resource group, VNet, DNS, monitoring, Key Vault
2. **Data (Phase 3):** SQL Server + Database, Storage Account
3. **Real-Time (Phase 4):** SignalR Service
4. **Compute (Phase 5):** App Service Plan, Function App, RBAC assignments
5. **Authentication (Phase 6):** Microsoft Entra External ID tenant (manual configuration)
6. **Application Deployment:** Deploy Function App code via Azure DevOps/GitHub Actions
7. **Mobile App Configuration:** Update mobile app with External ID tenant and Function App URL
8. **Testing:** End-to-end testing with mobile app

### Dependencies Summary

- **Key Vault** depends on VNet (for private endpoint)
- **SQL Server** depends on VNet, DNS, Key Vault (for secrets)
- **Storage Account** depends on VNet, DNS
- **SignalR Service** depends on VNet, DNS, Key Vault
- **Function App** depends on App Service Plan, VNet, Application Insights, Key Vault, SQL, Storage, SignalR
- **All Private Endpoints** depend on VNet subnets and Private DNS Zones

---

**End of Implementation Plan**
