$ErrorActionPreference = 'Continue'

Write-Host 'LeadsAI containers:' -ForegroundColor Cyan
docker ps -a --filter 'name=leadsai-' --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'

$containers = @(
    'leadsai-mongodb', 'leadsai-rabbitmq', 'leadsai-redis', 'leadsai-seq',
    'leadsai-identity-api', 'leadsai-platform-api', 'leadsai-api-gateway'
)

foreach ($container in $containers) {
    docker inspect $container *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[MISSING] $container" -ForegroundColor DarkYellow
        continue
    }

    $status = docker inspect --format '{{.State.Status}}{{if .State.Health}} / {{.State.Health.Status}}{{end}}' $container
    Write-Host "[$status] $container"
    if ($status -match 'exited|dead|unhealthy') {
        Write-Host "--- last logs: $container ---" -ForegroundColor Yellow
        docker logs $container --tail 80
    }
}

Write-Host 'Liveness endpoints:' -ForegroundColor Cyan
foreach ($endpoint in @('http://localhost:8081/health', 'http://localhost:8080/health', 'http://localhost:10000/health')) {
    try {
        $response = Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 5
        Write-Host "[$($response.StatusCode)] $endpoint" -ForegroundColor Green
    }
    catch {
        Write-Host "[DOWN] $endpoint - $($_.Exception.Message)" -ForegroundColor Red
    }
}
