# GitHub Actions Azure Deployment Setup

This guide walks through configuring GitHub Actions to deploy the Meal Plan Organizer infrastructure and Azure Functions to Azure.

## Prerequisites

- GitHub repository access with admin permissions
- Azure subscription (`46c4ffee-60f3-4055-813c-3037475e62ca`)
- Azure CLI installed locally (for creating service principal)
- Access to create service principals in your Azure tenant

## Step 1: Create Azure Service Principal

The service principal allows GitHub Actions to authenticate and deploy to Azure without storing passwords.

### Option A: Using Azure CLI (Recommended)

Run these commands in PowerShell or your terminal:

```powershell
# Set variables
$subscriptionId = "46c4ffee-60f3-4055-813c-3037475e62ca"
$servicePrincipalName = "gh-mealplan-organizer-deploy"

# Login to Azure
az login

# Set the subscription
az account set --subscription $subscriptionId

# Create the service principal
$spOutput = az ad sp create-for-rbac `
  --name $servicePrincipalName `
  --role Contributor `
  --scopes /subscriptions/$subscriptionId

# Parse the JSON output
$sp = $spOutput | ConvertFrom-Json

# Save the output for the next steps
$sp | ConvertTo-Json | Out-File sp-credentials.json
Write-Host "Service Principal created successfully"
Write-Host "Client ID: $($sp.appId)"
Write-Host "Tenant ID: $($sp.tenant)"
Write-Host ""
Write-Host "These values are also saved in sp-credentials.json"
```

This creates a service principal with **Contributor** role on the subscription.

**Important:** The output field names are:
- `appId` (not `clientId`)
- `tenant` (not `tenantId`)
- `password` (the client secret)

### Option B: Using Azure Portal

1. Go to [Azure portal](https://portal.azure.com) → Azure Active Directory → App registrations
2. Click "New registration"
3. Enter name: `gh-mealplan-organizer-deploy`
4. Click "Register"
5. Go to "Certificates & secrets" → "Client secrets"
6. Click "New client secret"
7. Enter description: "GitHub Actions deployment"
8. Copy the secret value (you won't see it again)
9. **Find Subscriptions:** In Azure Portal, click the search bar at the top, type "Subscriptions", and select the result
10. Click on your subscription: `46c4ffee-60f3-4055-813c-3037475e62ca`
11. Go to "Access control (IAM)" in the left sidebar
12. Click "Add role assignment"
13. Select role: "Contributor"
14. On the "Members" tab, click "Select members"
15. Search for your app registration name: `gh-mealplan-organizer-deploy`
16. Click on it to select it
17. Click "Review + assign"

## Step 2: Add GitHub Secrets

Add the Azure credentials to your GitHub repository as secrets.

1. Go to your repository → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add these secrets:

### Required Secrets

| Secret Name | Value | Source |
|---|---|---|
| `AZURE_CLIENT_ID` | Service principal App ID (`appId` from output) | From `$sp.appId` output above |
| `AZURE_TENANT_ID` | Service principal Tenant ID (`tenant` from output) | From `$sp.tenant` output above |
| `AZURE_SUBSCRIPTION_ID` | `46c4ffee-60f3-4055-813c-3037475e62ca` | Subscription ID |

### Optional Secrets (for explicit authentication)

If using client secret authentication instead of OIDC:

| Secret Name | Value |
|---|---|
| `AZURE_CLIENT_SECRET` | Service principal secret (Option B above) |

**Note:** OIDC (OpenID Connect) is more secure than using client secrets. The workflows use OIDC by default.

### Adding Secrets via GitHub CLI

```bash
# If using GitHub CLI
gh secret set AZURE_CLIENT_ID --body "<client-id>"
gh secret set AZURE_TENANT_ID --body "<tenant-id>"
gh secret set AZURE_SUBSCRIPTION_ID --body "46c4ffee-60f3-4055-813c-3037475e62ca"
```

### Adding Secrets via Portal

For each secret:
1. Click "New repository secret"
2. Name: (from table above)
3. Secret: (paste value)
4. Click "Add secret"

## Step 3: Configure OIDC (Recommended for Security)

GitHub recommends using OpenID Connect (OIDC) instead of client secrets for authentication.

### Setup OIDC Trust Relationship

1. Go to Azure portal → Azure Active Directory → App registrations
2. Search for your app (`gh-mealplan-organizer-deploy`)
3. Click on it
4. Go to "Certificates & secrets" → "Federated credentials"
5. Click "Add credential"
6. Configure the **Issuer details**:
   - **Federated credential scenario:** GitHub Actions deploying Azure resources
   - **Organization:** Your GitHub organization or username
   - **Repository:** `MealPlanOrganizer`
   - **Entity type:** Branch
   - **GitHub branch name:** `main`
7. Configure the **Credential details**:
   - **Name:** `github-actions-main`
   - **Description:** `GitHub Actions deployment credential for main branch`
8. Click "Add"

Repeat for other branches if needed (e.g., `develop`, `staging`).

Example for staging branch:
- **Name:** `github-actions-staging`
- **Description:** `GitHub Actions deployment credential for staging branch`

## Step 4: Verify Setup

Test the authentication by running a workflow manually:

1. Go to your repository → Actions
2. Click "Deploy Infrastructure (Bicep)"
3. Click "Run workflow"
4. Select Phase: `1` (just test)
5. Click "Run workflow"
6. Wait for completion (should succeed if secrets are configured correctly)

## Step 5: Understand the Workflows

### `deploy-infrastructure.yml`

Deploys Azure infrastructure (Phases 1-5 Bicep templates).

**Triggers:**
- Manual dispatch with phase selection
- Automatic on `main` branch push to `infra/bicep/` path

**Phases:**
1. **Phase 1:** VNet, Subnets, Log Analytics, Application Insights
2. **Phase 2:** Key Vault with RBAC
3. **Phase 3:** SQL Server, SQL Database, Storage Account
4. **Phase 4:** SignalR Service (Free_F1)
5. **Phase 5:** Function App, Function App Storage

**Outputs:**
- Each phase captures deployment outputs
- Artifacts uploaded for review
- Summary posted to workflow summary

### `deploy-functions.yml`

Builds, tests, and deploys Azure Functions code.

**Triggers:**
- Manual dispatch
- Automatic on `main` branch push to `src/MealPlanOrganizer.Functions/` path

**Steps:**
1. Build .NET 8 project
2. Run unit tests (if present)
3. Publish Function App
4. Apply database migrations (EF Core)
5. Health check Function App endpoint
6. List deployed functions

## Step 6: Deploy to Azure

### First Time Deployment

Deploy infrastructure phases in order:

```bash
# Option 1: Via GitHub UI
# Actions tab → Deploy Infrastructure → Run workflow → Phase: all

# Option 2: Via GitHub CLI
gh workflow run deploy-infrastructure.yml -f phase=all
```

Wait for Phase 1-5 to complete, then deploy functions:

```bash
# Deploy functions
# Actions tab → Deploy Azure Functions → Run workflow
gh workflow run deploy-functions.yml
```

### Subsequent Deployments

- **Infrastructure changes:** Commit to `infra/bicep/` → Auto-triggers workflow
- **Function changes:** Commit to `src/MealPlanOrganizer.Functions/` → Auto-triggers workflow
- **Manual deployments:** Use Actions tab → Run workflow

## Troubleshooting

### "Azure Login Failed"

**Issue:** `invalid_request` or `invalid_client` error

**Solutions:**
1. Verify secrets are correctly entered (no extra spaces)
2. Verify OIDC federated credential is created
3. Verify service principal has Contributor role on subscription
4. Check token expiration in `AZURE_CLIENT_SECRET`

### "Function App Deployment Failed"

**Issue:** Deployment fails with `FunctionAppDeploymentFailed`

**Solutions:**
1. Verify Function App exists (`func-mealplan-organizer`)
2. Verify storage account exists for Function runtime
3. Check Azure portal → Function App → Deployment logs
4. Verify .NET version matches (should be 8.0)

### "Database Migration Failed"

**Issue:** EF Core migrations don't apply

**Solutions:**
1. Verify SQL Server and database exist
2. Verify `SqlConnectionString` secret in Key Vault
3. Check Function App can access Key Vault (managed identity)
4. Manually verify connection:
   ```powershell
   az keyvault secret show --vault-name kv-mealplan-org --name SqlConnectionString
   ```

### "Health Check Timeout"

**Issue:** Function App health check times out

**Solutions:**
1. Function App may need 2-3 minutes to start
2. Check if there's a startup error in Application Insights
3. Verify storage account and Key Vault are accessible
4. Check for RBAC permission issues

## Manual Testing After Deployment

After successful deployment, test the Function App:

```powershell
# Get Function App URL
$functionAppUrl = "https://func-mealplan-organizer.azurewebsites.net"

# Test health endpoint
$response = Invoke-WebRequest "$functionAppUrl/api/health" -UseBasicParsing
Write-Host "Health status: $($response.StatusCode)"

# Test CreateRecipe function
$recipe = @{
    title = "Test Recipe"
    description = "A test recipe"
    cuisinetType = "Italian"
    prepTimeMinutes = 15
    cookTimeMinutes = 30
    servings = 4
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "$functionAppUrl/api/CreateRecipe" `
  -Method POST `
  -Body $recipe `
  -ContentType "application/json" `
  -UseBasicParsing
Write-Host $response.Content
```

## Environment Variables Reference

**Workflow Environments:**

| Variable | Value | Usage |
|---|---|---|
| `AZURE_SUBSCRIPTION_ID` | From secret | Azure CLI commands |
| `AZURE_RESOURCE_GROUP` | `rg-mealplan-organizer` | Resource group name |
| `AZURE_FUNCTION_APP_NAME` | `func-mealplan-organizer` | Function App deployment |
| `AZURE_LOCATION` | `canadacentral` | Azure region |
| `DOTNET_VERSION` | `8.0.x` | .NET SDK version |

## Security Best Practices

1. ✅ **Use OIDC** instead of client secrets
2. ✅ **Limit service principal scope** to specific resource group (if possible)
3. ✅ **Rotate credentials** every 6 months
4. ✅ **Review audit logs** in Azure Activity Log
5. ✅ **Never commit secrets** to repository
6. ✅ **Use branch protection** to require approvals for main
7. ✅ **Monitor workflow runs** in Actions tab

## Additional Resources

- [GitHub Actions Azure Login](https://github.com/azure/login)
- [GitHub Actions Azure Functions Deploy](https://github.com/Azure/functions-action)
- [Azure CLI Deployment Command](https://learn.microsoft.com/cli/azure/deployment/group)
- [OIDC Token Authentication](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [Azure App Registration Documentation](https://learn.microsoft.com/azure/active-directory/develop/app-objects-and-service-principals)

## Next Steps

1. ✅ Create Azure service principal (Step 1 of 3 above)
2. ✅ Add GitHub secrets (Step 2 of 3 above)
3. ✅ Configure OIDC federated credentials (Step 3 of 3 above)
4. ✅ Run test deployment (verify setup works)
5. Deploy Phase 1-5 infrastructure
6. Deploy Function App code
7. Monitor deployment logs
8. Test Function App endpoints
