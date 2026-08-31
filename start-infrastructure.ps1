$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # SQL Server is intentionally not started here. API containers connect to
    # the Windows SQL Express TCP endpoint configured in the root .env file.
    docker compose up -d --build mongodb rabbitmq redis consul seq portainer api-gateway
    if ($LASTEXITCODE -ne 0) { throw 'Infrastructure startup failed.' }
    docker compose ps mongodb rabbitmq redis consul seq portainer api-gateway
}
finally {
    Pop-Location
}
