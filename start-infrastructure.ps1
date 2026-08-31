$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    docker compose up -d --build mongodb rabbitmq redis consul seq portainer api-gateway
    if ($LASTEXITCODE -ne 0) { throw 'Infrastructure startup failed.' }
    docker compose ps mongodb rabbitmq redis consul seq portainer api-gateway
}
finally {
    Pop-Location
}
