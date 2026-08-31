$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'start-infrastructure.ps1')
& (Join-Path $PSScriptRoot 'start-apis.ps1')
& (Join-Path $PSScriptRoot 'status-all.ps1')
