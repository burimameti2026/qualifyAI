$ErrorActionPreference = 'Stop'
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'
$envFile = Join-Path $PSScriptRoot '.env'

Push-Location $hostingRoot
try {
    if (Test-Path $envFile) {
        docker compose --env-file $envFile down --remove-orphans
    }
    else {
        docker compose down --remove-orphans
    }
}
finally {
    Pop-Location
}

Push-Location $PSScriptRoot
try {
    docker compose down --remove-orphans
}
finally {
    Pop-Location
}
