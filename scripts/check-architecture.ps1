$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$violations = New-Object System.Collections.Generic.List[string]

function Get-ServiceRoots {
    $roots = @(
        (Join-Path $repoRoot 'src/Business'),
        (Join-Path $repoRoot 'src/Services/Identity')
    )

    $servicesRoot = Join-Path $repoRoot 'src/Services'
    if (Test-Path $servicesRoot) {
        Get-ChildItem $servicesRoot -Directory |
            Where-Object { $_.Name -ne 'Identity' } |
            ForEach-Object { $roots += $_.FullName }
    }

    return $roots | Where-Object { Test-Path $_ }
}

Write-Host 'Checking DbContext boundaries...'
foreach ($serviceRoot in Get-ServiceRoots) {
    $contextFiles = Get-ChildItem $serviceRoot -Recurse -Filter '*.cs' |
        Where-Object {
            $content = Get-Content $_.FullName -Raw
            $content -match 'class\s+\w+DbContext\b[^\r\n]*:\s*(?:IdentityDbContext|DbContext)' -or
            $content -match 'class\s+\w+DbContext\s*\([^\)]*\)\s*:\s*(?:IdentityDbContext|DbContext)'
        }

    $count = @($contextFiles).Count
    $name = Split-Path $serviceRoot -Leaf
    Write-Host "  $name : $count DbContext class(es)"

    if ($count -gt 5) {
        $violations.Add("$name defines $count DbContext classes. Maximum allowed is 5; default should be 1.")
    }
}

Write-Host 'Checking API persistence leaks...'
$legacyApiAllowList = @(
    'src/Business/QualifyAI.Api/ModuleEndpoints.cs',
    'src/Business/QualifyAI.Api/ExtendedAdminEndpoints.cs',
    'src/Business/QualifyAI.Api/PublicChatEndpoints.cs'
)

$apiFiles = Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -match '[\\/]\.?(?:[^\\/]+\.)?Api[\\/]' -or $_.DirectoryName -match '\.Api(?:[\\/]|$)' }

foreach ($file in $apiFiles) {
    $relative = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\\', '/')
    if ($relative -in $legacyApiAllowList) {
        continue
    }

    $content = Get-Content $file.FullName -Raw
    if ($content -match '\b\w*DbContext\b') {
        if ($file.Name -ne 'Program.cs') {
            $violations.Add("API layer persistence leak: $relative references a DbContext directly. Use command/query + repository contracts.")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host ''
    Write-Host 'Architecture guard failed:' -ForegroundColor Red
    $violations | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'Architecture guard passed.' -ForegroundColor Green
