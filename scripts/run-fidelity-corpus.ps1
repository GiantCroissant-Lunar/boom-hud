[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$ManifestGlob = "fidelity/corpus/*.json",
    [string]$UnityExe = "",
    [string]$OutputPath = "build/fidelity/corpus/summary.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot))
{
    $scriptRoot = Split-Path -Parent $PSCommandPath
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
}

$manifestSearchPath = if ([System.IO.Path]::IsPathRooted($ManifestGlob))
{
    $ManifestGlob
}
else
{
    Join-Path $RepoRoot $ManifestGlob
}

$manifests = Get-ChildItem -Path $manifestSearchPath | Sort-Object FullName
if ($manifests.Count -eq 0)
{
    throw "No fidelity manifests matched '$manifestSearchPath'."
}

$runnerPath = Join-Path $RepoRoot "scripts/run-pen-remotion-unity-fidelity.ps1"
$corpusSummary = @()

foreach ($manifest in $manifests)
{
    Write-Host ""
    Write-Host "==> Running fidelity manifest $($manifest.FullName)"

    $arguments = @(
        "-NoProfile",
        "-File", $runnerPath,
        "-RepoRoot", $RepoRoot,
        "-ManifestPath", $manifest.FullName
    )

    if (-not [string]::IsNullOrWhiteSpace($UnityExe))
    {
        $arguments += @("-UnityExe", $UnityExe)
    }

    & pwsh @arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "Fidelity run failed for '$($manifest.FullName)'."
    }

    $manifestJson = Get-Content $manifest.FullName -Raw | ConvertFrom-Json -Depth 20
    $artifactsRoot = if ([System.IO.Path]::IsPathRooted([string]$manifestJson.artifactsRoot))
    {
        [string]$manifestJson.artifactsRoot
    }
    else
    {
        Join-Path $RepoRoot ([string]$manifestJson.artifactsRoot)
    }

    $summaryPath = Join-Path $artifactsRoot "reports/summary.json"
    if (-not (Test-Path $summaryPath))
    {
        throw "Expected summary at '$summaryPath' was not found."
    }

    $summaryEntries = @(Get-Content $summaryPath -Raw | ConvertFrom-Json -Depth 20)
    foreach ($entry in $summaryEntries)
    {
        $corpusSummary += [pscustomobject]@{
            manifest = $manifest.BaseName
            id = [string]$entry.id
            pair = [string]$entry.pair
            overallSimilarityPercent = [double]$entry.overallSimilarityPercent
            passedThreshold = [bool]$entry.passedThreshold
        }
    }
}

$outputAbsolutePath = if ([System.IO.Path]::IsPathRooted($OutputPath))
{
    $OutputPath
}
else
{
    Join-Path $RepoRoot $OutputPath
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputAbsolutePath) | Out-Null
$corpusSummary | ConvertTo-Json -Depth 8 | Set-Content -Path $outputAbsolutePath

$failed = @($corpusSummary | Where-Object { -not $_.passedThreshold })
if ($failed.Count -gt 0)
{
    throw "Fidelity corpus failed. See '$outputAbsolutePath'."
}

Write-Host ""
Write-Host "Fidelity corpus passed."
Write-Host "Summary: $outputAbsolutePath"
