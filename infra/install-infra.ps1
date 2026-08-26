$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Host "Created infra\.env from .env.example." -ForegroundColor Yellow
}

$network = docker network ls --filter "name=^qualifyai-infra$" --format "{{.Name}}"
if ($network -ne "qualifyai-infra") {
    docker network create qualifyai-infra | Out-Null
}

docker compose --env-file .env up -d
if ($LASTEXITCODE -ne 0) { throw "QualifyAI infrastructure failed to start." }

$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $leader = Invoke-RestMethod -Uri "http://localhost:8500/v1/status/leader" -TimeoutSec 2
        if ($leader) { $ready = $true; break }
    } catch {}
    Start-Sleep -Seconds 2
}
if (-not $ready) { throw "Consul did not become healthy." }

Write-Host "QualifyAI infrastructure started." -ForegroundColor Green
Write-Host "Consul:    http://localhost:8500"
Write-Host "RabbitMQ:  http://localhost:15672"
Write-Host "Seq:       http://localhost:5341"
Write-Host "Portainer: https://localhost:9443"
Write-Host "SQL:       localhost:1433"
Write-Host "MongoDB:   localhost:27017"
docker compose --env-file .env ps
