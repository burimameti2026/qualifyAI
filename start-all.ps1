$ErrorActionPreference = 'Stop'

$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed: $($Arguments -join ' ')" }
}

function Assert-RunningContainer {
    param([string]$Container)
    Start-Sleep -Seconds 5
    $running = (& docker inspect --format '{{.State.Running}}' $Container 2>$null)
    if ($running -ne 'true') {
        Write-Host "[FAILED] $Container stopped during startup" -ForegroundColor Red
        & docker logs $Container --tail 200
        throw "$Container is not running."
    }
    Write-Host "[OK] $Container is running" -ForegroundColor Green
}

Push-Location $hostingRoot
try {
    Write-Host 'Starting infrastructure...' -ForegroundColor Cyan
    Invoke-Compose up -d mongodb rabbitmq redis consul seq portainer
    Assert-RunningContainer qualifyai-mongodb
    Assert-RunningContainer qualifyai-rabbitmq
    Assert-RunningContainer qualifyai-redis
    Assert-RunningContainer qualifyai-consul

    Write-Host 'Building and starting APIs...' -ForegroundColor Cyan
    Invoke-Compose up -d --build --no-deps identity-api
    Assert-RunningContainer qualifyai-identity-api
    Invoke-Compose up -d --build --no-deps platform-api
    Assert-RunningContainer qualifyai-platform-api
    Invoke-Compose up -d --build --no-deps api-gateway
    Assert-RunningContainer qualifyai-api-gateway
    Invoke-Compose ps -a
}
finally {
    Pop-Location
}

Write-Host 'QualifyAI is running. Gateway: http://localhost:10000' -ForegroundColor Green
