param(
    [Parameter(Mandatory=$true)][int]$Port,
    [Parameter(Mandatory=$true)][string]$CodeFile
)

$code = (Get-Content -LiteralPath $CodeFile) -join "`n"
$body = @{
    jsonrpc = '2.0'
    id = 1
    method = 'tools/call'
    params = @{
        name = 'execute_code'
        arguments = @{
            code = $code
            safety_checks = $true
            skip_refresh = $true
        }
    }
} | ConvertTo-Json -Depth 30 -Compress

try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/mcp" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 60
    $response.Content
} catch {
    Write-Output ("ERROR=" + $_.Exception.Message)
    exit 1
}
