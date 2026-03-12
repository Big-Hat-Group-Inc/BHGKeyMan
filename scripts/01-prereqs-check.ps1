param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [string]$TenantId
)

$ErrorActionPreference = 'Stop'

Write-Host "Checking Azure CLI installation..."
$azVersion = az version --output json 2>$null
if (-not $azVersion) {
    throw "Azure CLI (az) is required but was not found in PATH."
}

Write-Host "Checking Azure login context..."
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    throw "No active Azure login context found. Run: az login"
}

Write-Host "Setting subscription context..."
az account set --subscription $SubscriptionId

$current = az account show --output json | ConvertFrom-Json
if ($current.id -ne $SubscriptionId) {
    throw "Failed to switch to subscription '$SubscriptionId'."
}

if ($current.tenantId -ne $TenantId) {
    throw "Current tenant '$($current.tenantId)' does not match required tenant '$TenantId'."
}

Write-Host "Prerequisite check passed for subscription '$SubscriptionId' and tenant '$TenantId'."
