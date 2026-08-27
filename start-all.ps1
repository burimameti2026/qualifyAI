$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    docker compose up -d --build
    docker compose ps
} finally {
    Pop-Location
}
Write-Host "QualifyAI APIs and infrastructure started." -ForegroundColor Green
