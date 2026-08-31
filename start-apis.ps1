$ErrorActionPreference = 'Stop'
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'
$envFile = Join-Path $PSScriptRoot '.env'

if (-not (Test-Path $envFile)) {
    throw 'Root .env is missing. Copy .env.example to .env and add your development values.'
}

Push-Location $hostingRoot
try {
    docker compose --env-file $envFile up -d --build identity-api platform-api
    if ($LASTEXITCODE -ne 0) { throw 'API startup failed.' }

    Start-Sleep -Seconds 5
    docker compose ps -a identity-api platform-api
}
finally {
    Pop-Location
}
