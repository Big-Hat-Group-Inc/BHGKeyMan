param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $true)]
    [string]$LogAnalyticsWorkspaceResourceId
)

$ErrorActionPreference = 'Stop'

az account set --subscription $SubscriptionId
$vaultId = az keyvault show --name $KeyVaultName --resource-group $ResourceGroupName --query id --output tsv

$settingName = "kv-diagnostics"
$existing = az monitor diagnostic-settings list --resource $vaultId --query "value[?name=='$settingName'].name" --output tsv

if (-not $existing) {
    az monitor diagnostic-settings create --name $settingName --resource $vaultId --workspace $LogAnalyticsWorkspaceResourceId --logs '[{"category":"AuditEvent","enabled":true}]' --metrics '[{"category":"AllMetrics","enabled":true}]' | Out-Null

    Write-Host "Diagnostic setting '$settingName' created."
}
else {
    Write-Host "Diagnostic setting '$settingName' already exists."
}
