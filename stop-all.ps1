$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try { docker compose down --remove-orphans } finally { Pop-Location }
