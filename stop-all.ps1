$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '.env'

Push-Location $PSScriptRoot
try {
    if (Test-Path $envFile) {
        foreach ($project in @('renova', 'leadsai', 'leadsai-apps', 'qualifyai', 'qualifyai-apps')) {
            docker compose --project-name $project --env-file $envFile down --remove-orphans 2>$null
        }
    }
    else {
        docker compose --project-name renova down --remove-orphans
    }

    foreach ($container in @(
        'leadsai-mongodb','leadsai-rabbitmq','leadsai-redis','leadsai-seq',
        'leadsai-identity-api','leadsai-platform-api','leadsai-api-gateway','leadsai-portainer',
        'qualifyai-mongodb','qualifyai-rabbitmq','qualifyai-redis','qualifyai-seq',
        'qualifyai-identity-api','qualifyai-platform-api','qualifyai-api-gateway','qualifyai-portainer'
    )) {
        docker rm -f $container 2>$null | Out-Null
    }
}
finally {
    Pop-Location
}
