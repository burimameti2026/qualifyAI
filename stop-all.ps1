$ErrorActionPreference = "Stop"
$hostingRoot = Join-Path $PSScriptRoot 'src/Infrastructure/QualifyAI.Infrastructure.Hosting'
Push-Location $hostingRoot
try { docker compose down --remove-orphans } finally { Pop-Location }
