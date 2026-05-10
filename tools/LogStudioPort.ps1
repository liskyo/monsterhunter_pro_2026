param([string]$Root)
$logPath = Join-Path $Root "debug-0f54bc.log"
$t = Test-NetConnection -ComputerName 127.0.0.1 -Port 54323 -WarningAction SilentlyContinue
$payload = [ordered]@{
    sessionId    = "0f54bc"
    hypothesisId = "H3"
    location     = "start-dev.bat:port_check"
    message      = "tcp_54323"
    data         = @{ TcpTestSucceeded = [bool]$t.TcpTestSucceeded }
    timestamp    = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
}
Add-Content -LiteralPath $logPath -Encoding utf8 -Value ($payload | ConvertTo-Json -Compress -Depth 5)
