Write-Warning 'start-apis.ps1 is deprecated: the backend now has one Compose project. Starting the complete stack.'
& (Join-Path $PSScriptRoot 'start-all.ps1')
