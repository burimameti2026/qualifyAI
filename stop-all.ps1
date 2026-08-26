Push-Location "$PSScriptRoot\services"; docker compose down --remove-orphans; Pop-Location
Push-Location "$PSScriptRoot\infra"; docker compose --env-file .env down; Pop-Location
