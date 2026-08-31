$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    docker compose up -d mongodb rabbitmq redis consul seq portainer
    if ($LASTEXITCODE -ne 0) { throw 'Infrastructure startup failed.' }
    docker compose ps mongodb rabbitmq redis consul seq portainer
}
finally {
    Pop-Location
}
