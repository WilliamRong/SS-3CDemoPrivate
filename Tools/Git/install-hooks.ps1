$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$sourceHook = Join-Path $PSScriptRoot "pre-commit"
$targetHook = Join-Path $repoRoot ".git\hooks\pre-commit"

if (-not (Test-Path (Join-Path $repoRoot ".git"))) {
    Write-Error "Not a git repository: $repoRoot"
    exit 1
}

Copy-Item -Path $sourceHook -Destination $targetHook -Force
Write-Host "Installed pre-commit hook to $targetHook"
