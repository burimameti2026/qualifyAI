$ErrorActionPreference = 'Stop'
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'

Push-Location $hostingRoot
try {
    docker compose up -d mongodb rabbitmq redis consul seq portainer
    if ($LASTEXITCODE -ne 0) { throw 'Infrastructure startup failed.' }
    docker compose ps mongodb rabbitmq redis consul seq portainer
}
finally {
    Pop-Location
}
