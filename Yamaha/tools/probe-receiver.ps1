param(
    [Parameter(Mandatory = $false)]
    [string]$HostAddress = "192.168.78.22",

    [int]$Port = 80
)

$base = "http://${HostAddress}:${Port}/YamahaExtendedControl/v1"

function Invoke-Yxc([string]$Path) {
    $uri = "$base/$Path"
    Write-Host "GET $uri"
    Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 5
}

Write-Host "=== getDeviceInfo ==="
Invoke-Yxc "system/getDeviceInfo" | ConvertTo-Json -Depth 8

Write-Host "=== getStatus (main) ==="
Invoke-Yxc "main/getStatus" | ConvertTo-Json -Depth 8
