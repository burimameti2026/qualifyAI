$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet restore .\RaiseLead.sln
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

dotnet build .\RaiseLead.sln -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Push-Location .\admin\qualifyai-admin
try {
    if (Test-Path package-lock.json) { npm ci } else { npm install }
    if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Angular build failed" }
} finally { Pop-Location }

Write-Host "RaiseLead source build completed." -ForegroundColor Green
