$ErrorActionPreference = 'Stop'
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'
$envFile = Join-Path $PSScriptRoot '.env'

if (-not (Test-Path $envFile)) {
    throw 'Root .env is missing. Copy .env.example to .env and add your development values.'
}

$envValues = @{}
Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith('#')) {
        $parts = $line -split '=', 2
        if ($parts.Count -eq 2) {
            $envValues[$parts[0].Trim()] = $parts[1].Trim()
        }
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
    $canReachSql = Test-NetConnection -ComputerName 'localhost' -Port $sqlPort -InformationLevel Quiet
    if (-not $canReachSql) {
        throw "SQL Server is not listening on localhost:$sqlPort. Enable TCP/IP for SQLEXPRESS, assign this fixed TCP port, restart SQL Server (SQLEXPRESS), and allow the port through Windows Firewall."
    }
}
elseif ($dbServer -match '\\') {
    throw 'Docker cannot reliably use a Windows named instance such as .\SQLEXPRESS. Set DB_SERVER=host.docker.internal,<fixed-tcp-port> in the root .env file.'
}

Push-Location $hostingRoot
try {
    docker compose --env-file $envFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'API compose configuration is invalid.' }

    docker compose --env-file $envFile up -d --build identity-api platform-api
    if ($LASTEXITCODE -ne 0) { throw 'API startup failed.' }

    Start-Sleep -Seconds 5
    docker compose ps -a identity-api platform-api
}
finally {
    Pop-Location
}
