$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '.env'

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
    if (-not $envValues.ContainsKey($requiredName) -or
        [string]::IsNullOrWhiteSpace($envValues[$requiredName])) {
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
    docker compose --env-file $envFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose configuration is invalid.' }

    # Remove containers created by the former second Compose project. This is
    # idempotent and prevents container-name conflicts on the first migration.
    docker compose --project-name qualifyai-apps --env-file $envFile down --remove-orphans
    if ($LASTEXITCODE -ne 0) { throw 'Legacy Compose project cleanup failed.' }

    docker compose --env-file $envFile up -d --build --remove-orphans
    if ($LASTEXITCODE -ne 0) { throw 'QualifyAI startup failed.' }

    & (Join-Path $PSScriptRoot 'status-all.ps1')
}
finally {
    Pop-Location
}
