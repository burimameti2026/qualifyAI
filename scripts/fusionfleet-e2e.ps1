$ErrorActionPreference = "Stop"

$identity = $env:QUALIFYAI_IDENTITY_URL ?? "http://localhost:8081"
$api = $env:QUALIFYAI_API_URL ?? "http://localhost:8080"
$adminTenant = $env:QUALIFYAI_ADMIN_TENANT ?? "demo"
$adminEmail = $env:QUALIFYAI_ADMIN_EMAIL ?? "admin@demo.local"
$adminPassword = $env:QUALIFYAI_ADMIN_PASSWORD ?? "Admin123!ChangeMe"
$ownerEmail = $env:FUSIONFLEET_OWNER_EMAIL ?? "fusionfleet.master@local.test"
$ownerPassword = $env:FUSIONFLEET_OWNER_PASSWORD ?? "FusionFleet123!ChangeMe"

function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw "ASSERTION FAILED: $message" } Write-Host "[OK] $message" -ForegroundColor Green }
function Get-Token([string]$tenant, [string]$email, [string]$password) {
    $token = Invoke-RestMethod -Method Post -Uri "$identity/connect/token" -ContentType "application/x-www-form-urlencoded" -Body @{
        grant_type = "password"; client_id = "qualifyai-admin"; username = $email; password = $password; tenant = $tenant
        scope = "openid profile email offline_access qualifyai-api"
    }
    Assert-True (-not [string]::IsNullOrWhiteSpace($token.access_token)) "token issued for $tenant/$email"
    return $token.access_token
}

$adminToken = Get-Token $adminTenant $adminEmail $adminPassword
$adminHeaders = @{ Authorization = "Bearer $adminToken"; "X-Tenant" = $adminTenant }

$existing = Invoke-RestMethod -Method Get -Uri "$identity/api/identity/tenants" -Headers $adminHeaders
$tenant = $existing | Where-Object { $_.slug -eq "fusionfleet" } | Select-Object -First 1
if (-not $tenant) {
    $tenant = Invoke-RestMethod -Method Post -Uri "$identity/api/identity/tenants/provision" -Headers $adminHeaders -ContentType "application/json" -Body (@{
        name = "FusionFleetClientTenant"; slug = "fusionfleet"; contactEmail = $ownerEmail; plan = "growth"
        startsAtUtc = [DateTime]::UtcNow.ToString("o"); expiresAtUtc = $null; gracePeriodEndsAtUtc = $null; maxUsers = 10
        modules = @("crm","golden_pipeline","automation","settings","billing")
        ownerEmail = $ownerEmail; ownerPassword = $ownerPassword; ownerFirstName = "FusionFleet"; ownerLastName = "MasterUser"
    } | ConvertTo-Json -Depth 5))
    $tenantId = $tenant.tenantId
    Write-Host "[OK] Created FusionFleet tenant $tenantId" -ForegroundColor Green
} else {
    $tenantId = $tenant.id
    Write-Host "[OK] FusionFleet tenant already exists: $tenantId" -ForegroundColor Green
}

$ownerToken = Get-Token "fusionfleet" $ownerEmail $ownerPassword
$headers = @{ Authorization = "Bearer $ownerToken"; "X-Tenant" = "fusionfleet" }

$runtime = Invoke-RestMethod -Method Get -Uri "$api/api/tenant-runtime" -Headers $headers
Assert-True ($runtime.slug -eq "fusionfleet") "tenant runtime resolves to FusionFleet workspace"
Assert-True ($runtime.status -eq "Active" -or $runtime.status -eq "active") "FusionFleet tenant is active"
$expected = @("crm","golden_pipeline","automation","settings","billing")
foreach ($module in $expected) { Assert-True ($runtime.enabledModules -contains $module) "module enabled: $module" }
foreach ($module in @("knowledge","analytics","integrations","ticketing","inbox")) { Assert-True (-not ($runtime.enabledModules -contains $module)) "module disabled/hidden: $module" }

$provisioning = Invoke-RestMethod -Method Get -Uri "$api/api/admin/tenants/$tenantId/provisioning" -Headers $adminHeaders
$golden = $provisioning.modules | Where-Object { $_.moduleCode -eq "golden_pipeline" }
Assert-True ($golden.status -eq "completed") "Golden Pipeline provisioning completed"

$billing = Invoke-RestMethod -Method Get -Uri "$api/api/billing/tenants/$tenantId" -Headers $adminHeaders
Assert-True ($null -ne $billing) "tenant billing endpoint is reachable"
$invoices = Invoke-RestMethod -Method Get -Uri "$api/api/billing/tenants/$tenantId/invoices" -Headers $adminHeaders
Assert-True ($null -ne $invoices) "tenant invoice endpoint is reachable"

$options = Invoke-RestMethod -Method Get -Uri "$api/api/real-workspace/options" -Headers $headers
$template = $options.templates | Where-Object { $_.id -eq "fusionfleet-promotion" } | Select-Object -First 1
Assert-True ($null -ne $template) "FusionFleet workspace template is available"
Assert-True ($template.prospects.Count -eq 5) "FusionFleet template contains five starter prospects"

$draft = Invoke-RestMethod -Method Post -Uri "$api/api/real-workspace/prepare" -Headers $headers -ContentType "application/json" -Body (@{ useCaseId="logistics"; templateId="fusionfleet-promotion"; name="FusionFleet" } | ConvertTo-Json)
Assert-True ($draft.status -eq "draft") "real workspace draft created"
Assert-True ($draft.prospects.Count -eq 5) "draft contains five editable prospects"

$draft.prospects[0].companyName = "FusionFleet Test Prospect"
$draft.selectedProspectIds = @($draft.prospects[0].id)
$saved = Invoke-RestMethod -Method Put -Uri "$api/api/real-workspace" -Headers $headers -ContentType "application/json" -Body (@{
    workspaceId=$draft.workspaceId; name="FusionFleet"; prospects=$draft.prospects; selectedProspectIds=$draft.selectedProspectIds
} | ConvertTo-Json -Depth 8)
Assert-True ($saved.selectedProspectIds.Count -eq 1) "edited prospect selection persisted"

$activated = Invoke-RestMethod -Method Post -Uri "$api/api/real-workspace/activate" -Headers $headers -ContentType "application/json" -Body ($draft.workspaceId | ConvertTo-Json)
Assert-True ($activated.status -eq "active") "FusionFleet real workspace activated"

$overview = Invoke-RestMethod -Method Get -Uri "$api/api/acquisition/overview" -Headers $headers
Assert-True ($overview.discovered -ge 1) "activated prospect is visible to acquisition"

$pipeline = Invoke-RestMethod -Method Get -Uri "$api/api/golden-pipeline" -Headers $headers
Assert-True ($pipeline.stages.Count -ge 4) "Golden Pipeline stages are available"

Write-Host "FusionFleet E2E verification complete." -ForegroundColor Cyan
