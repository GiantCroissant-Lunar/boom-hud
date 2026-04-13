[CmdletBinding()]
param(
    [string]$FixtureRefsRoot = "build/fixture-refs",
    [string]$OutputJson = "build/fixture-refs/reference-similarity-leaderboard.json",
    [string]$OutputMarkdown = "build/fixture-refs/reference-similarity-leaderboard.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath([string]$PathValue)
{
    if ([string]::IsNullOrWhiteSpace($PathValue))
    {
        return $PathValue
    }

    if ([System.IO.Path]::IsPathRooted($PathValue))
    {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $PathValue))
}

$fixtureRoot = Resolve-AbsolutePath $FixtureRefsRoot
$jsonOutPath = Resolve-AbsolutePath $OutputJson
$markdownOutPath = Resolve-AbsolutePath $OutputMarkdown

$summaryFiles = @(
    Get-ChildItem -Path $fixtureRoot -Recurse -Filter '*.reference-summary.json' -File -ErrorAction SilentlyContinue
)

$rows =
    foreach ($summaryFile in $summaryFiles)
    {
        $summary = Get-Content $summaryFile.FullName -Raw | ConvertFrom-Json -Depth 20
        [pscustomobject]@{
            fixture = [System.IO.Path]::GetFileNameWithoutExtension([System.IO.Path]::GetDirectoryName($summaryFile.FullName))
            rootId = [string]$summary.rootId
            rootName = [string]$summary.rootName
            primaryMode = [string]$summary.primaryReferenceSimilarity.mode
            primaryOverallSimilarityPercent = [double]$summary.primaryReferenceSimilarity.overallSimilarityPercent
            fullScreenOverallSimilarityPercent = [double]$summary.fullScreen.overallSimilarityPercent
            uiOnlyOverallSimilarityPercent = if ($null -ne $summary.uiOnly) { [double]$summary.uiOnly.overallSimilarityPercent } else { $null }
            summaryPath = $summaryFile.FullName
            inputPen = [string]$summary.inputPen
            referenceImage = [string]$summary.referenceImage
        }
    }

$orderedRows = @($rows | Sort-Object primaryOverallSimilarityPercent -Descending)

$leaderboard = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("o")
    preferredMetric = "uiOnly when available; otherwise fullScreen"
    fixtureCount = $orderedRows.Count
    entries = $orderedRows
}

New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($jsonOutPath)) | Out-Null
$leaderboard | ConvertTo-Json -Depth 20 | Set-Content -Path $jsonOutPath -Encoding UTF8

$markdownLines = @(
    "# Reference Similarity Leaderboard",
    "",
    "Primary metric: ``uiOnly`` when available; otherwise ``fullScreen``.",
    "",
    "| Rank | Fixture | Primary | Full Screen | UI Only | |",
    "|---:|---|---:|---:|---:|---|"
)

$rank = 1
foreach ($row in $orderedRows)
{
    $uiOnlyDisplay = if ($null -ne $row.uiOnlyOverallSimilarityPercent) { ('{0:N2}%' -f $row.uiOnlyOverallSimilarityPercent) } else { "-" }
    $markdownLines += "| $rank | $($row.rootName) | $('{0:N2}%' -f $row.primaryOverallSimilarityPercent) ($($row.primaryMode)) | $('{0:N2}%' -f $row.fullScreenOverallSimilarityPercent) | $uiOnlyDisplay | [$($row.rootId)]($($row.summaryPath -replace '\\','/')) |"
    $rank++
}

$markdownLines -join [Environment]::NewLine | Set-Content -Path $markdownOutPath -Encoding UTF8

$leaderboard | ConvertTo-Json -Depth 20
