$ErrorActionPreference = "Stop"
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'
Push-Location $hostingRoot
try {
    docker compose up -d --build
    docker compose ps
} finally {
    Pop-Location
}
Write-Host "QualifyAI APIs and infrastructure started." -ForegroundColor Green
