<#
.SYNOPSIS
    Verifies that BoomHud.Cli does not reference *.Generated namespaces.

.DESCRIPTION
    This script enforces the "no-leak" rule: command handlers in BoomHud.Cli
    should only consume domain wrappers, never generated DTOs directly.

    See docs/dev/SCHEMA_DTO_DOMAIN.md for the full pattern.

.EXAMPLE
    pwsh -NoProfile -File scripts/verify-no-dto-leak.ps1

.NOTES
    Exit codes:
      0 - No leaks found
      1 - Generated namespace references found in CLI
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$cliPath = Join-Path $PSScriptRoot '..' 'dotnet' 'src' 'BoomHud.Cli'

if (-not (Test-Path $cliPath)) {
    Write-Error "CLI directory not found: $cliPath"
    exit 1
}

Write-Host "Checking for Generated namespace references in BoomHud.Cli..." -ForegroundColor Cyan

# Search for .Generated namespace references
$leaks = Get-ChildItem -Path $cliPath -Filter '*.cs' -Recurse |
    Select-String -Pattern '\.Generated' -SimpleMatch

if ($leaks) {
    Write-Host ""
    Write-Host "ERROR: BoomHud.Cli must not reference *.Generated namespaces." -ForegroundColor Red
    Write-Host "Use domain wrappers instead of generated DTOs." -ForegroundColor Red
    Write-Host ""
    Write-Host "Violations found:" -ForegroundColor Yellow
    
    foreach ($leak in $leaks) {
        Write-Host "  $($leak.Path):$($leak.LineNumber): $($leak.Line.Trim())" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "See docs/dev/SCHEMA_DTO_DOMAIN.md for the schema -> DTO -> domain pattern." -ForegroundColor Cyan
    exit 1
}

Write-Host "OK: No Generated namespace references found in CLI." -ForegroundColor Green
exit 0
