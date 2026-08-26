$ErrorActionPreference = "Stop"

$network = docker network ls --filter name=^qualifyai-infra$ --format "{{.Name}}"
if ($network -ne "qualifyai-infra") {
    throw "Infrastructure network 'qualifyai-infra' is missing. Run ..\\infra\\install-infra.ps1 first."
}
if (!(Test-Path .env)) { Copy-Item .env.example .env }

$services = @(
    "business-api",
    "identity-api",
    "automation-api",
    "notifications-api",
    "knowledge-api",
    "aiorchestration-api",
    "integrations-api",
    "admin-ui"
)

foreach ($service in $services) {
    Write-Host "`n=== Building $service ===" -ForegroundColor Cyan
    docker compose --env-file .env build $service --progress=plain
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $service." }
}

Write-Host "`n=== Starting application stack ===" -ForegroundColor Cyan
docker compose --env-file .env up -d --no-build
if ($LASTEXITCODE -ne 0) { throw "QualifyAI application stack failed to start." }

docker compose ps
Write-Host "UI:       http://localhost:8088"
Write-Host "Business: http://localhost:8080/swagger"
Write-Host "Identity: http://localhost:8081/swagger"
