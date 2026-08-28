$ErrorActionPreference = 'Stop'

$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed: $($Arguments -join ' ')" }
}

function Wait-HealthyContainer {
    param([string]$Container, [int]$TimeoutSeconds = 180)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $state = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $Container 2>$null)
        if ($state -eq 'healthy' -or $state -eq 'running') {
            Write-Host "[OK] $Container is $state" -ForegroundColor Green
            return
        }
        if ($state -eq 'unhealthy' -or $state -eq 'exited' -or $state -eq 'dead') {
            Write-Host "[FAILED] $Container is $state" -ForegroundColor Red
            & docker logs $Container --tail 120
            throw "$Container failed to become healthy."
        }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    & docker logs $Container --tail 120
    throw "Timed out waiting for $Container."
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
    Wait-HealthyContainer qualifyai-mongodb
    Wait-HealthyContainer qualifyai-rabbitmq
    Wait-HealthyContainer qualifyai-redis
    Wait-HealthyContainer qualifyai-consul

    Write-Host 'Building and starting APIs...' -ForegroundColor Cyan
    Invoke-Compose up -d --build identity-api
    Assert-RunningContainer qualifyai-identity-api
    Invoke-Compose up -d --build platform-api
    Assert-RunningContainer qualifyai-platform-api
    Invoke-Compose up -d --build api-gateway
    Assert-RunningContainer qualifyai-api-gateway
    Invoke-Compose ps -a
}
finally {
    Pop-Location
}

Write-Host 'QualifyAI is running. Gateway: http://localhost:10000' -ForegroundColor Green
