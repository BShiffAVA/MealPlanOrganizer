# Phase 3 Deployment Script
# Deploys SQL Server and Storage Account with private endpoints

param(
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId = "46c4ffee-60f3-4055-813c-3037475e62ca",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-mealplan-organizer",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "canadacentral"
)

# Ensure we have az CLI
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 3: Data Tier Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Azure Authentication
Write-Host "Checking Azure Authentication..." -ForegroundColor Yellow
$currentUser = az account show --query "user.name" -o tsv
$currentSub = az account show --query "name" -o tsv
Write-Host "Authenticated as: $currentUser" -ForegroundColor Green
Write-Host "Current subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Get or create Azure AD Service Principal
Write-Host "Getting Azure AD Service Principal..." -ForegroundColor Yellow
$adSp = az ad sp list --display-name "mealplan-sql-admin" --query "[0].id" -o tsv 2>$null

if ([string]::IsNullOrEmpty($adSp)) {
    Write-Host "Service principal not found. Creating..." -ForegroundColor Cyan
    $adSp = az ad sp create-for-rbac --name "mealplan-sql-admin" --skip-assignment --query "id" -o tsv
    Write-Host "Created service principal: $adSp" -ForegroundColor Green
} else {
    Write-Host "Found service principal: $adSp" -ForegroundColor Green
}
Write-Host ""

# Update parameters file
Write-Host "Updating parameters file..." -ForegroundColor Yellow
$paramsFile = Join-Path (Get-Location) "phase-3.parameters.json"
$params = Get-Content $paramsFile | ConvertFrom-Json
$params.parameters.sqlAadAdminObjectId.value = $adSp
$params | ConvertTo-Json -Depth 10 | Set-Content $paramsFile
Write-Host "Updated sqlAadAdminObjectId: $adSp" -ForegroundColor Green
Write-Host ""

# Deploy
Write-Host "Deploying Phase 3..." -ForegroundColor Yellow
$deploymentName = "phase-3-$(Get-Date -Format 'yyyyMMddHHmmss')"
Write-Host "Deployment Name: $deploymentName" -ForegroundColor Cyan
Write-Host ""

az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file main.bicep `
    --parameters @phase-3.parameters.json `
    --parameters location=$Location `
    --name $deploymentName

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Verifying deployment..." -ForegroundColor Yellow
    
    $deployment = az deployment group show `
        --name $deploymentName `
        --resource-group $ResourceGroupName `
        --query "properties.provisioningState" `
        -o tsv
    
    Write-Host "Deployment State: $deployment" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Phase 3 Deployment Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Deployment failed!" -ForegroundColor Red
    exit 1
}
