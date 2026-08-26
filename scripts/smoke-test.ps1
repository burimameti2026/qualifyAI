$ErrorActionPreference = "Stop"
$tenant = "demo"
$email = "admin@demo.local"
$password = "Admin123!"

$token = Invoke-RestMethod -Method Post -Uri "http://localhost:8081/connect/token" -ContentType "application/x-www-form-urlencoded" -Body @{
    grant_type = "password"
    client_id = "qualifyai-admin"
    username = $email
    password = $password
    tenant = $tenant
    scope = "openid profile email offline_access qualifyai-api"
}
if (-not $token.access_token) { throw "Identity did not return access_token" }
Write-Host "[OK] Identity login" -ForegroundColor Green

$headers = @{ Authorization = "Bearer $($token.access_token)"; "X-Tenant" = $tenant }
$dashboard = Invoke-RestMethod -Uri "http://localhost:8080/api/dashboard" -Headers $headers
Write-Host "[OK] Business dashboard" -ForegroundColor Green

$users = Invoke-RestMethod -Uri "http://localhost:8081/users/" -Headers $headers
Write-Host "[OK] Identity users: $($users.Count)" -ForegroundColor Green

$ui = Invoke-WebRequest -Uri "http://localhost:8088" -UseBasicParsing
if ($ui.StatusCode -ne 200) { throw "UI not healthy" }
Write-Host "[OK] Angular UI" -ForegroundColor Green

Write-Host "Smoke test complete." -ForegroundColor Cyan
