param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $true)]
    [string]$PrincipalObjectId,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Reader','SecretsUser','SecretsOfficer')]
    [string]$Persona
)

$ErrorActionPreference = 'Stop'

az account set --subscription $SubscriptionId
$vaultId = az keyvault show --name $KeyVaultName --resource-group $ResourceGroupName --query id --output tsv

switch ($Persona) {
    'Reader' { $roleName = 'Key Vault Reader' }
    'SecretsUser' { $roleName = 'Key Vault Secrets User' }
    'SecretsOfficer' { $roleName = 'Key Vault Secrets Officer' }
}

$existing = az role assignment list --assignee-object-id $PrincipalObjectId --scope $vaultId --role "$roleName" --query "[].id" --output tsv
if (-not $existing) {
    az role assignment create --assignee-object-id $PrincipalObjectId --assignee-principal-type User --role "$roleName" --scope $vaultId | Out-Null
    Write-Host "Assigned '$roleName' to principal '$PrincipalObjectId'."
}
else {
    Write-Host "Role assignment already exists for '$roleName'."
}
