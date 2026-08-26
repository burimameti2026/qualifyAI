$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
docker compose --env-file .env down
