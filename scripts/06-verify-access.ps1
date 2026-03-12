param(
    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $false)]
    [string]$TestSecretName,

    [Parameter(Mandatory = $false)]
    [string]$TestSecretValue = "verification-value"
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($TestSecretName)) {
    $TestSecretName = "verification-$([Guid]::NewGuid().ToString('N'))"
}

Write-Host "Verifying read/list access..."
az keyvault secret list --vault-name $KeyVaultName --maxresults 5 --output table | Out-Null

try {
    Write-Host "Verifying write access by setting temporary secret '$TestSecretName'..."
    $TestSecretValue | az keyvault secret set --vault-name $KeyVaultName --name $TestSecretName --value '@-' | Out-Null

    Write-Host "Verifying retrieval of latest version..."
    $retrieved = az keyvault secret show --vault-name $KeyVaultName --name $TestSecretName --query value --output tsv
    if ($retrieved -ne $TestSecretValue) {
        throw "Retrieved value did not match expected test value."
    }

    Write-Host "Access verification passed for list, set, and get operations."
}
finally {
    try {
        az keyvault secret delete --vault-name $KeyVaultName --name $TestSecretName | Out-Null
        Write-Host "Deleted temporary verification secret '$TestSecretName'."
    }
    catch {
        Write-Warning "Verification secret '$TestSecretName' could not be deleted automatically. Remove it manually."
    }
}
