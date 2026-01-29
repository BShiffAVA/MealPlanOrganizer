# Phase 1: Foundation & Networking Deployment Guide

## Overview
This directory contains the Bicep Infrastructure as Code (IaC) templates for **Phase 1** of the Meal Plan Organizer Azure deployment.

### Phase 1 Objectives
-  **TASK-001**: Create resource group (`rg-mealplan-organizer`)
-  **TASK-002**: Deploy Log Analytics Workspace with 30-day retention
-  **TASK-003**: Deploy Application Insights linked to Log Analytics
-  **TASK-004**: Create Virtual Network with 2 subnets for private connectivity
-  **TASK-005**: Deploy 4 Private DNS Zones for SQL, Key Vault, Storage, and SignalR

## Files in this Directory

### Core Templates
- **main.bicep** (8.6 KB)
  - Main Bicep template orchestrating all Phase 1 resources
  - Includes proper resource dependencies and outputs
  - All resources tagged for cost tracking and management

- **phase-1.parameters.json** (1.2 KB)
  - Parameter values for Canada Central deployment
  - Configuration for prod environment
  - Customizable resource names and network settings

## Architecture Overview

### Resource Group
- **Name**: `rg-mealplan-organizer`
- **Location**: Canada Central (canadacentral)
- **Organizational Unit**: Family meal planning infrastructure

### Networking
- **Virtual Network**: `vnet-mealplan-organizer`
  - **Address Space**: 10.0.0.0/16
  - **Subnets**:
    - `snet-integration` (10.0.2.0/24) - For App Service/Function App VNet integration
    - `snet-private-endpoints` (10.0.1.0/24) - For private endpoints to Azure services

### Monitoring & Observability
- **Log Analytics Workspace**: `log-mealplan-organizer`
  - **SKU**: PerGB2018
  - **Retention**: 30 days
  - **Purpose**: Centralized logging for all Phase 2-6 services

- **Application Insights**: `appi-mealplan-organizer`
  - **Application Type**: Web
  - **Linked to**: Log Analytics Workspace
  - **Purpose**: APM for Function App and mobile app telemetry

### Private DNS Zones (for secure private connectivity)
1. `privatelink.database.windows.net` - SQL Server private endpoints
2. `privatelink.vaultcore.azure.net` - Key Vault private endpoints
3. `privatelink.blob.core.windows.net` - Storage Account private endpoints
4. `privatelink.signalr.net` - SignalR Service private endpoints

## Deployment Instructions

### Prerequisites
1. Azure CLI installed and authenticated
   ```powershell
   az login
   az account set --subscription <subscription-id>
   ```

2. Bicep CLI (v0.14+) or Azure CLI with Bicep support
   ```powershell
   az bicep version
   ```

### Step 1: Validate the Template
```powershell
# Validate Bicep syntax and ARM template generation
az bicep build --file infra/bicep/phase-1/main.bicep --outfile phase-1.template.json
```

### Step 2: Create Resource Group
```powershell
az group create `
  --name rg-mealplan-organizer `
  --location canadacentral
```

### Step 3: Deploy Phase 1 Infrastructure
```powershell
az deployment group create `
  --name phase-1-foundation-networking `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-1/main.bicep `
  --parameters infra/bicep/phase-1/phase-1.parameters.json
```

### Step 4: Retrieve Deployment Outputs
```powershell
az deployment group show `
  --name phase-1-foundation-networking `
  --resource-group rg-mealplan-organizer `
  --query properties.outputs
```

## Key Outputs
After successful deployment, the following outputs will be available:
- `logAnalyticsWorkspaceId` - Resource ID for Log Analytics Workspace
- `applicationInsightsId` - Resource ID for Application Insights
- `applicationInsightsInstrumentationKey` - Instrumentation key for app integration
- `virtualNetworkId` - Resource ID for Virtual Network
- `integrationSubnetId` - Resource ID for integration subnet
- `privateEndpointSubnetId` - Resource ID for private endpoints subnet
- `privateDnsZoneSqlId` through `privateDnsZonesSignalRId` - Private DNS Zone resource IDs

## Estimated Costs (Monthly)
- **Log Analytics Workspace**: ~$0.80 (PerGB2018, assuming 5GB/month)
- **Application Insights**: ~$0.30 (First 1GB/month free, then $2.30/GB)
- **Virtual Network**: ~$0.35 (VNet itself is free, charges for connectivity)
- **Private DNS Zones**: ~$0.50 (4 zones  $0.125/zone)
- **Total Phase 1**: ~$2.00/month (minimal tier estimate)

**Note**: Costs increase when Phase 2-6 resources (SQL, Storage, Key Vault, SignalR, App Service, Function App) are deployed in subsequent phases.

## Network Security Considerations
 Private DNS zones configured for secure name resolution
 Private endpoint subnets prepared for Phase 2-6 resources
 Service endpoints enabled on integration subnet for Key Vault, SQL, Storage
 VNet integration subnet ready for Function App and App Service

## Next Steps
After Phase 1 deployment succeeds:

1. **Phase 2** - Deploy Key Vault and SQL Database
2. **Phase 3** - Deploy Storage Account and Blob containers
3. **Phase 4** - Deploy SignalR Service
4. **Phase 5** - Deploy Function App and App Service Plan
5. **Phase 6** - Deploy Mobile App and Entra External ID tenant

Each phase builds on Phase 1's networking and monitoring foundation.

## Troubleshooting

### Deployment Fails with "Quota Exceeded"
- Check your Azure subscription quotas for vNets and private DNS zones
- Increase quotas in Azure Portal > Subscriptions > Usage + quotas

### Private Endpoints Not Resolving
- Verify DNS zone VNet links were created successfully
- Confirm custom DNS servers are not interfering
- Test with `nslookup` or `Resolve-DnsName` from within VNet

### Log Analytics Workspace Already Exists
- Change the `logAnalyticsName` parameter value in phase-1.parameters.json
- Ensure uniqueness within the Azure region

## Parameters Reference

| Parameter | Default | Description |
|-----------|---------|-------------|
| location | canadacentral | Azure region for all resources |
| environment | prod | Environment name (prod/staging/dev) |
| logAnalyticsName | log-mealplan-organizer | Workspace name |
| logAnalyticsRetentionDays | 30 | Data retention (7-730 days) |
| vnetAddressSpace | 10.0.0.0/16 | Virtual Network CIDR block |
| integrationSubnetPrefix | 10.0.2.0/24 | App integration subnet CIDR |
| privateEndpointSubnetPrefix | 10.0.1.0/24 | Private endpoints subnet CIDR |

## Support & Documentation
- Azure Verified Modules: https://aka.ms/AVM
- Bicep Documentation: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/
- MealPlanOrganizer INFRA: See INFRA.MealPlanOrganizer.md for complete specification
