param(
    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $true)]
    [string]$SecretsJsonPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SecretsJsonPath)) {
    throw "Secrets JSON file '$SecretsJsonPath' was not found."
}

$pattern = '^[A-Za-z][0-9A-Za-z-]{0,126}$'
$secrets = Get-Content $SecretsJsonPath -Raw | ConvertFrom-Json

foreach ($entry in $secrets.PSObject.Properties) {
    $name = $entry.Name
    $value = [string]$entry.Value

    if ($name -notmatch $pattern) {
        throw "Secret name '$name' is invalid."
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Secret value for '$name' is empty."
    }

    $value | az keyvault secret set --vault-name $KeyVaultName --name $name --value '@-' | Out-Null
    Write-Host "Seeded secret '$name'."
}

Write-Host "Secret seeding completed."
