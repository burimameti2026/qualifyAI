$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '.env'

Push-Location $PSScriptRoot
try {
    if (Test-Path $envFile) {
        docker compose --project-name qualifyai-apps --env-file $envFile down --remove-orphans
        docker compose --env-file $envFile down --remove-orphans
    }
    else {
        docker compose down --remove-orphans
    }
}
finally {
    Pop-Location
}
