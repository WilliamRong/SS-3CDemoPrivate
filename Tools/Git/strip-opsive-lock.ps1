param(
    [string]$LockFile = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Packages\packages-lock.json")
)

if (-not (Test-Path $LockFile)) {
    Write-Error "Lock file not found: $LockFile"
    exit 1
}

$content = Get-Content -Raw -Path $LockFile
if ($content -notmatch 'com\.opsive\.') {
    exit 0
}

$json = $content | ConvertFrom-Json
$changed = $false
foreach ($key in @($json.dependencies.PSObject.Properties.Name)) {
    if ($key -like 'com.opsive.*') {
        $json.dependencies.PSObject.Properties.Remove($key)
        $changed = $true
    }
}

if (-not $changed) {
    exit 0
}

$json | ConvertTo-Json -Depth 100 | Set-Content -Path $LockFile -Encoding utf8NoBOM
Write-Host "Removed Opsive entries from $LockFile"
