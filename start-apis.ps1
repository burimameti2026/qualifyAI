$ErrorActionPreference = 'Stop'
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'

function Start-ApiContainer {
    param([string]$Service, [string]$Container)

    docker compose up -d --build --no-deps $Service
    if ($LASTEXITCODE -ne 0) { throw "$Service build/start failed." }

    Start-Sleep -Seconds 5
    $running = docker inspect --format '{{.State.Running}}' $Container 2>$null
    if ($running -ne 'true') {
        docker logs $Container --tail 200
        throw "$Container stopped during startup."
    }

    Write-Host "[OK] $Container is running" -ForegroundColor Green
}

Push-Location $hostingRoot
try {
    Start-ApiContainer identity-api qualifyai-identity-api
    Start-ApiContainer platform-api qualifyai-platform-api
    Start-ApiContainer api-gateway qualifyai-api-gateway
    docker compose ps identity-api platform-api api-gateway
}
finally {
    Pop-Location
}
