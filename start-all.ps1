$ErrorActionPreference = "Stop"
Push-Location "$PSScriptRoot\infra"
try { .\install-infra.ps1 } finally { Pop-Location }
Push-Location "$PSScriptRoot\services"
try { .\install-api.ps1 } finally { Pop-Location }
Write-Host "QualifyAI full stack started." -ForegroundColor Green
