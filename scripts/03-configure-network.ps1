param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $false)]
    [string]$VnetName,

    [Parameter(Mandatory = $false)]
    [string]$SubnetName,

    [Parameter(Mandatory = $false)]
    [string]$PrivateDnsZoneResourceGroup,

    [switch]$UsePrivateEndpoint,
    [switch]$UseFirewallMode
)

$ErrorActionPreference = 'Stop'

if ($UsePrivateEndpoint) {
    if (-not $VnetName -or -not $SubnetName -or -not $PrivateDnsZoneResourceGroup) {
        throw "VnetName, SubnetName, and PrivateDnsZoneResourceGroup are required for private endpoint mode."
    }

    $peName = "$KeyVaultName-pe"
    $connectionName = "$KeyVaultName-connection"
    $dnsZoneName = "privatelink.vaultcore.azure.net"
    $zoneLinkName = "$VnetName-kv-dns-link"

    Write-Host "Creating private endpoint for '$KeyVaultName'..."
    az network private-endpoint create --name $peName --resource-group $ResourceGroupName --vnet-name $VnetName --subnet $SubnetName --private-connection-resource-id $(az keyvault show --name $KeyVaultName --resource-group $ResourceGroupName --query id -o tsv) --group-id vault --connection-name $connectionName | Out-Null

    Write-Host "Ensuring private DNS zone '$dnsZoneName'..."
    $zoneId = az network private-dns zone show --name $dnsZoneName --resource-group $PrivateDnsZoneResourceGroup --query id -o tsv 2>$null
    if (-not $zoneId) {
        az network private-dns zone create --resource-group $PrivateDnsZoneResourceGroup --name $dnsZoneName | Out-Null
    }

    $existingLink = az network private-dns link vnet show --resource-group $PrivateDnsZoneResourceGroup --zone-name $dnsZoneName --name $zoneLinkName --query id -o tsv 2>$null
    if (-not $existingLink) {
        az network private-dns link vnet create --resource-group $PrivateDnsZoneResourceGroup --zone-name $dnsZoneName --name $zoneLinkName --virtual-network $VnetName --registration-enabled false | Out-Null
    }

    az network private-endpoint dns-zone-group create --resource-group $ResourceGroupName --endpoint-name $peName --name default --private-dns-zone $dnsZoneName --zone-name default | Out-Null

    az keyvault update --name $KeyVaultName --resource-group $ResourceGroupName --public-network-access Disabled | Out-Null
    Write-Host "Private endpoint mode configured. Public network access disabled."
}
elseif ($UseFirewallMode) {
    Write-Host "Configuring firewall mode for '$KeyVaultName'..."
    az keyvault update --name $KeyVaultName --resource-group $ResourceGroupName --public-network-access Enabled --default-action Deny | Out-Null
    Write-Host "Firewall mode configured. Add explicit IP/network rules separately as needed."
}
else {
    throw "Specify either -UsePrivateEndpoint or -UseFirewallMode."
}
