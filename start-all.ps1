$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '.env'
$composeArgs = @('--project-name', 'renova', '--env-file', $envFile)

if (-not (Test-Path $envFile)) {
    throw 'Root .env is missing. Copy .env.example to .env and set DB_SERVER, DB_USER and DB_PASSWORD.'
}

$envValues = @{}
Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith('#')) {
        $parts = $line -split '=', 2
        if ($parts.Count -eq 2) { $envValues[$parts[0].Trim()] = $parts[1].Trim() }
    }
}

foreach ($requiredName in @('DB_SERVER', 'DB_USER', 'DB_PASSWORD')) {
    if (-not $envValues.ContainsKey($requiredName) -or [string]::IsNullOrWhiteSpace($envValues[$requiredName])) {
        throw "$requiredName is missing from the root .env file."
    }
}

$dbServer = $envValues['DB_SERVER']
if ($dbServer -match '^host\.docker\.internal,(?<port>\d+)$') {
    $sqlPort = [int]$Matches['port']
    if (-not (Test-NetConnection -ComputerName 'localhost' -Port $sqlPort -InformationLevel Quiet)) {
        throw "SQL Express is not listening on localhost:$sqlPort. Enable TCP/IP, use a fixed port, restart SQL Server (SQLEXPRESS), and allow the port through Windows Firewall."
    }
}
elseif ($dbServer -match '\\') {
    throw 'A Linux container cannot use .\SQLEXPRESS. Use DB_SERVER=host.docker.internal,<fixed-tcp-port>.'
}

Push-Location $PSScriptRoot
try {
    docker compose @composeArgs config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Renova Docker Compose configuration is invalid.' }

    # Stop this stack and known legacy Compose projects left behind by the rename.
    foreach ($legacyProject in @('leadsai', 'leadsai-apps', 'qualifyai', 'qualifyai-apps')) {
        docker compose --project-name $legacyProject --env-file $envFile down --remove-orphans 2>$null
    }

    # Remove legacy containers that used fixed container names and therefore are
    # not necessarily owned by the current Compose project label.
    $legacyContainers = @(
        'leadsai-mongodb','leadsai-rabbitmq','leadsai-redis','leadsai-seq',
        'leadsai-identity-api','leadsai-platform-api','leadsai-api-gateway','leadsai-portainer',
        'qualifyai-mongodb','qualifyai-rabbitmq','qualifyai-redis','qualifyai-seq',
        'qualifyai-identity-api','qualifyai-platform-api','qualifyai-api-gateway','qualifyai-portainer'
    )
    foreach ($container in $legacyContainers) {
        docker rm -f $container 2>$null | Out-Null
    }

    docker compose @composeArgs up -d --build --remove-orphans
    if ($LASTEXITCODE -ne 0) { throw 'Renova startup failed.' }

    # Verify Docker DNS from the API container before declaring the stack ready.
    docker compose @composeArgs exec -T platform-api sh -c 'getent hosts rabbitmq >/dev/null 2>&1 && getent hosts redis >/dev/null 2>&1 && getent hosts mongodb >/dev/null 2>&1 && getent hosts identity-api >/dev/null 2>&1'
    if ($LASTEXITCODE -ne 0) { throw 'Renova Docker DNS check failed. Core services are not on the same Compose network.' }

    & (Join-Path $PSScriptRoot 'status-all.ps1')
}
finally {
    Pop-Location
}
