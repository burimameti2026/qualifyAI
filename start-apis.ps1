$ErrorActionPreference = 'Stop'
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'

Push-Location $hostingRoot
try {
    docker compose up -d --build --no-deps identity-api platform-api api-gateway
    if ($LASTEXITCODE -ne 0) { throw 'API startup failed.' }

    Start-Sleep -Seconds 5
    docker compose ps -a identity-api platform-api api-gateway
}
finally {
    Pop-Location
}
