param(
    [string]$HypothesisId = "H0",
    [string]$Message = "",
    [string]$Root = ""
)
if ([string]::IsNullOrEmpty($Root)) { $Root = Split-Path -Parent $PSScriptRoot }
$logPath = Join-Path $Root "debug-0f54bc.log"
$payload = [ordered]@{
    sessionId    = "0f54bc"
    hypothesisId = $HypothesisId
    location     = "start-dev.bat"
    message      = $Message
    data         = @{}
    timestamp    = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
}
$line = ($payload | ConvertTo-Json -Compress -Depth 4)
Add-Content -LiteralPath $logPath -Encoding utf8 -Value $line
