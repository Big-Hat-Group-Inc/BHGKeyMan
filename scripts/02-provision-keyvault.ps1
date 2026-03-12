param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName
)

$ErrorActionPreference = 'Stop'

az account set --subscription $SubscriptionId

$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq 'false') {
    Write-Host "Creating resource group '$ResourceGroupName'..."
    az group create --name $ResourceGroupName --location $Location | Out-Null
}

$kvId = az keyvault show --name $KeyVaultName --resource-group $ResourceGroupName --query id --output tsv 2>$null
if (-not $kvId) {
    Write-Host "Creating key vault '$KeyVaultName'..."
    az keyvault create --name $KeyVaultName --resource-group $ResourceGroupName --location $Location --enable-rbac-authorization true --enable-soft-delete true --retention-days 90 | Out-Null
}

Write-Host "Enabling purge protection..."
az keyvault update --name $KeyVaultName --resource-group $ResourceGroupName --enable-purge-protection true | Out-Null

$vaultUri = az keyvault show --name $KeyVaultName --resource-group $ResourceGroupName --query properties.vaultUri --output tsv
Write-Host "Key Vault provisioned and hardened: $vaultUri"
