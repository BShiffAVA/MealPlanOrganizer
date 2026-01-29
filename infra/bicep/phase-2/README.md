# Phase 2: Security & Secrets Management

## Overview

Phase 2 deploys Azure Key Vault with private endpoint connectivity, establishing secure secrets management infrastructure for the Meal Plan Organizer application. This phase enables centralized storage and management of sensitive configuration values (connection strings, credentials, API keys) that will be consumed by Phase 5 (Function App).

### Objectives

- **TASK-006**: Deploy Key Vault with RBAC authorization and network isolation
- **TASK-007**: Configure private endpoint for Key Vault in private endpoint subnet with DNS zone integration
- **TASK-008**: Enable diagnostic settings to Log Analytics for audit logging
- **TASK-009**: Verify Key Vault accessibility from private endpoint (testing step)

### Architecture

```
[Azure Virtual Network: 10.0.0.0/16]
           |
    [Subnet: Private Endpoints - 10.0.1.0/24]
           |
    [Private Endpoint: Key Vault]
           |
           ├─→ [Key Vault (Standard SKU)]
           │        └─→ Network ACLs: Deny all except Azure Services & Private Endpoint
           │        └─→ RBAC Authorization: Enabled
           │        └─→ Purge Protection: Enabled
           │        └─→ Soft Delete: 90 days
           |
           └─→ [Private DNS Zone: privatelink.vaultcore.azure.net]
                   └─→ Records pointing to private IP
           
                   ↓
           [Log Analytics Workspace]
                   └─→ Receives audit logs & metrics
```

## Prerequisites

- **Phase 1 Deployment**: Must complete Phase 1 (Foundation & Networking) first
- **Azure CLI**: Version 2.50+
- **Bicep CLI**: Version 0.20+
- **Required Permissions**: 
  - Subscription-level Owner or Contributor role
  - Ability to create/modify Key Vaults and Private Endpoints
  - Access to read Phase 1 deployment outputs

## Phase 1 Outputs Required

Phase 2 deployment depends on the following outputs from Phase 1:

| Output | Parameter Name | Example Value |
| --- | --- | --- |
| Virtual Network ID | `vnetResourceId` | `/subscriptions/{subId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer` |
| Private Endpoints Subnet ID | `privateEndpointSubnetId` | `/subscriptions/{subId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/virtualNetworks/vnet-mealplan-organizer/subnets/snet-private-endpoints` |
| Key Vault Private DNS Zone ID | `keyVaultPrivateDnsZoneId` | `/subscriptions/{subId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.Network/privateDnsZones/privatelink.vaultcore.azure.net` |
| Log Analytics Workspace ID | `logAnalyticsWorkspaceId` | `/subscriptions/{subId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.OperationalInsights/workspaces/log-mealplan-organizer` |

## Deployment Steps

### Step 1: Update Parameters with Phase 1 Outputs

After Phase 1 deployment, retrieve the outputs and update `phase-2.parameters.json`:

```powershell
# Get Phase 1 deployment outputs
$phase1Outputs = az deployment group show `
  --name phase-1-foundation-networking `
  --resource-group rg-mealplan-organizer `
  --query properties.outputs

# Extract individual outputs (use as values in phase-2.parameters.json)
$vnetId = $phase1Outputs.virtualNetworkId.value
$subnetId = $phase1Outputs.privateEndpointSubnetId.value
$dnsZoneId = $phase1Outputs.privateDnsZonesKeyVaultId.value
$logWorkspaceId = $phase1Outputs.logAnalyticsWorkspaceId.value
```

### Step 2: Validate Template

```powershell
# Validate Bicep syntax and arm template
bicep build infra/bicep/phase-2/main.bicep --stdout | `
  az deployment group validate `
    --resource-group rg-mealplan-organizer `
    --template-file /dev/stdin `
    --parameters infra/bicep/phase-2/phase-2.parameters.json
```

### Step 3: Deploy Phase 2

```powershell
# Deploy Phase 2 infrastructure
az deployment group create `
  --name phase-2-security-secrets `
  --resource-group rg-mealplan-organizer `
  --template-file infra/bicep/phase-2/main.bicep `
  --parameters infra/bicep/phase-2/phase-2.parameters.json
```

### Step 4: Retrieve Phase 2 Outputs

```powershell
# Get Phase 2 deployment outputs for use in Phase 3+
az deployment group show `
  --name phase-2-security-secrets `
  --resource-group rg-mealplan-organizer `
  --query properties.outputs `
  --output table
```

Expected outputs:
- `keyVaultId`: Resource ID for Key Vault
- `keyVaultName`: Name of Key Vault
- `keyVaultUri`: URI for Key Vault (https://kv-mealplan-org.vault.azure.net/)
- `privateEndpointId`: Resource ID for Private Endpoint
- `privateEndpointName`: Name of Private Endpoint

## Resource Details

### Key Vault Configuration

| Property | Value | Justification |
| --- | --- | --- |
| **Name** | kv-mealplan-org | Globally unique, descriptive |
| **SKU** | Standard | Sufficient for household app, cost-effective |
| **Tenant ID** | Azure AD tenant | Automatically set to deployment tenant |
| **RBAC Authorization** | Enabled | Modern authorization model, removes access policy management |
| **Public Network Access** | Disabled | All traffic requires private endpoint |
| **Soft Delete** | 90 days | Protects against accidental deletion |
| **Purge Protection** | Enabled | Prevents permanent deletion during soft delete period |
| **Vault for Deployment** | Enabled | Allows ARM template access |
| **Vault for Template Deployment** | Enabled | Allows template parameter references |
| **Network ACLs** | Deny by default, allow Azure Services | Restrictive security posture |

### Private Endpoint Configuration

| Property | Value | Description |
| --- | --- | --- |
| **Location** | canadacentral | Same region as Phase 1 |
| **Subnet** | snet-private-endpoints (10.0.1.0/24) | Private endpoint subnet from Phase 1 |
| **Service** | vault | Key Vault service endpoint |
| **DNS Zone** | privatelink.vaultcore.azure.net | Private DNS zone from Phase 1 |
| **Private IP** | Auto-assigned from subnet | Within 10.0.1.0/24 range |

### Diagnostic Settings

| Setting | Value | Purpose |
| --- | --- | --- |
| **Destination** | Log Analytics Workspace | Centralized audit logging |
| **Log Category** | AuditEvent | Tracks all access to secrets |
| **Metrics** | AllMetrics | Performance monitoring |
| **Retention** | 30 days (Log Analytics default) | Balances cost with audit trail length |

## Security Considerations

### Network Isolation
- ✅ Key Vault accessible ONLY via private endpoint
- ✅ Public internet access explicitly disabled
- ✅ Private endpoint in isolated subnet (10.0.1.0/24)
- ✅ Private DNS zone ensures name resolution within VNet

### Access Control
- ✅ RBAC authorization enabled (no legacy access policies)
- ✅ Managed identities used by Phase 5 Function App for authentication
- ✅ No secrets stored in code or configuration files
- ✅ All sensitive data retrieved from Key Vault at runtime

### Compliance & Audit
- ✅ All access logged to Log Analytics
- ✅ Soft delete enabled (90 days recovery window)
- ✅ Purge protection prevents accidental permanent deletion
- ✅ TLS 1.2 enforced for all connections

## Cost Estimation

### Phase 2 Monthly Costs (Canada Central)

| Resource | SKU/Tier | Estimated Cost |
| --- | --- | --- |
| Key Vault (Standard) | Standard | ~$0.34/month |
| Private Endpoint | Network Interface | ~$0.01/hour × 720 hours = ~$7.20/month |
| Private DNS Zone | 1 zone | ~$0.50/month |
| Diagnostic Logs | Log Analytics ingest | Included in Phase 1 workspace |
| **Total Phase 2** | | ~$8/month |

**Note**: Phase 1 + Phase 2 = ~$10-15/month (well within $50 budget)

## Troubleshooting

### Issue: Private Endpoint Creation Fails

**Symptom**: Deployment error "Subnet does not have required Microsoft.Network/virtualNetworks/subnets/join/action permission"

**Resolution**:
```powershell
# Ensure subnet exists and is accessible
az network vnet subnet show `
  --resource-group rg-mealplan-organizer `
  --vnet-name vnet-mealplan-organizer `
  --name snet-private-endpoints
```

### Issue: Cannot Connect to Key Vault from Private Endpoint

**Symptom**: Azure CLI or code cannot resolve vault URI

**Resolution**:
1. Verify private DNS zone is linked to VNet:
```powershell
az network private-dns zone virtual-network-link list `
  --zone-name privatelink.vaultcore.azure.net `
  --resource-group rg-mealplan-organizer
```

2. Test DNS resolution from a VM in the VNet:
```powershell
# From a VM in vnet-mealplan-organizer
nslookup kv-mealplan-org.vault.azure.net
# Should resolve to private IP (10.0.1.x)
```

### Issue: Diagnostic Settings Not Writing Logs

**Symptom**: No audit events appear in Log Analytics

**Resolution**:
1. Verify diagnostic setting is enabled:
```powershell
az monitor diagnostic-settings show `
  --name kv-mealplan-org-diag `
  --resource /subscriptions/{subId}/resourceGroups/rg-mealplan-organizer/providers/Microsoft.KeyVault/vaults/kv-mealplan-org
```

2. Check Log Analytics workspace is accessible:
```powershell
az monitor log-analytics workspace show `
  --resource-group rg-mealplan-organizer `
  --workspace-name log-mealplan-organizer
```

## Next Steps: Phase 3 (Data Tier)

Phase 3 will deploy:
- SQL Server with Azure AD authentication
- SQL Database (Basic tier, 2GB)
- Storage Account for recipe images
- Both services will reference secrets stored in this Phase 2 Key Vault

To proceed to Phase 3, ensure Phase 2 deployment completes successfully and all outputs are captured.

## Testing Verification

After deployment, verify Phase 2 by:

1. **Key Vault Accessibility**:
```powershell
# From Azure CLI (requires RBAC assignment)
az keyvault secret list --vault-name kv-mealplan-org
```

2. **Private Endpoint Resolution**:
```powershell
# From VM in VNet
nslookup kv-mealplan-org.vault.azure.net
# Should return private IP in 10.0.1.0/24 range
```

3. **Audit Logging**:
```powershell
# Query Log Analytics for Key Vault events
az monitor log-analytics query `
  --workspace log-mealplan-organizer `
  --query "AzureDiagnostics | where ResourceType == 'VAULTS' | top 10"
```

4. **Public Access Verification**:
```powershell
# Attempt connection from public internet (should fail)
curl https://kv-mealplan-org.vault.azure.net/secrets
# Expected: Connection timeout or denied
```

---

**End of Phase 2 Documentation**

For questions or issues, refer to Phase 1 README.md for the overall infrastructure context.
