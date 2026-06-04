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
        $uiMaskPixelIdentity =
            if ($null -ne $summary.uiOnly -and $summary.uiOnly.PSObject.Properties["strictMaskPixelIdentityPercent"])
            {
                [double]$summary.uiOnly.strictMaskPixelIdentityPercent
            }
            elseif ($null -ne $summary.uiOnly -and $summary.uiOnly.PSObject.Properties["pixelIdentityPercent"])
            {
                [double]$summary.uiOnly.pixelIdentityPercent
            }
            else
            {
                $null
            }
        [pscustomobject]@{
            fixture = [System.IO.Path]::GetFileNameWithoutExtension([System.IO.Path]::GetDirectoryName($summaryFile.FullName))
            rootId = [string]$summary.rootId
            rootName = [string]$summary.rootName
            primaryMode = [string]$summary.primaryReferenceSimilarity.mode
            primaryOverallSimilarityPercent = [double]$summary.primaryReferenceSimilarity.overallSimilarityPercent
            fullScreenPixelIdentityPercent = [double]$summary.fullScreen.pixelIdentityPercent
            fullScreenOverallSimilarityPercent = [double]$summary.fullScreen.overallSimilarityPercent
            uiOnlyMaskPixelIdentityPercent = $uiMaskPixelIdentity
            uiOnlyPixelIdentityPercent = if ($null -ne $summary.uiOnly) { [double]$summary.uiOnly.pixelIdentityPercent } else { $null }
            uiOnlyOverallSimilarityPercent = if ($null -ne $summary.uiOnly) { [double]$summary.uiOnly.overallSimilarityPercent } else { $null }
            summaryPath = $summaryFile.FullName
            inputPen = [string]$summary.inputPen
            referenceImage = [string]$summary.referenceImage
        }
    }

$orderedRows = @($rows | Sort-Object primaryOverallSimilarityPercent -Descending)

$leaderboard = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("o")
    preferredMetric = "pixel identity inside explicit ui mask rectangles when available; otherwise fullScreen pixel identity"
    fixtureCount = $orderedRows.Count
    entries = $orderedRows
}

New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($jsonOutPath)) | Out-Null
$leaderboard | ConvertTo-Json -Depth 20 | Set-Content -Path $jsonOutPath -Encoding UTF8

$markdownLines = @(
    "# Reference Similarity Leaderboard",
    "",
    "Primary metric: strict pixel identity inside explicit ``uiOnly`` mask rectangles when available; otherwise strict pixel identity in ``fullScreen``.",
    "",
    "| Rank | Fixture | Primary | Full Screen Pixel | Full Screen Heuristic | UI Mask Pixel | UI Canvas Pixel | UI Heuristic | |",
    "|---:|---|---:|---:|---:|---:|---:|---:|---|"
)

$rank = 1
foreach ($row in $orderedRows)
{
    $fullScreenPixelDisplay = if ($null -ne $row.fullScreenPixelIdentityPercent) { ('{0:N2}%' -f $row.fullScreenPixelIdentityPercent) } else { "-" }
    $fullScreenHeuristicDisplay = if ($null -ne $row.fullScreenOverallSimilarityPercent) { ('{0:N2}%' -f $row.fullScreenOverallSimilarityPercent) } else { "-" }
    $uiMaskPixelDisplay = if ($null -ne $row.uiOnlyMaskPixelIdentityPercent) { ('{0:N2}%' -f $row.uiOnlyMaskPixelIdentityPercent) } else { "-" }
    $uiPixelDisplay = if ($null -ne $row.uiOnlyPixelIdentityPercent) { ('{0:N2}%' -f $row.uiOnlyPixelIdentityPercent) } else { "-" }
    $uiHeuristicDisplay = if ($null -ne $row.uiOnlyOverallSimilarityPercent) { ('{0:N2}%' -f $row.uiOnlyOverallSimilarityPercent) } else { "-" }
    $markdownLines += "| $rank | $($row.rootName) | $('{0:N2}%' -f $row.primaryOverallSimilarityPercent) ($($row.primaryMode)) | $fullScreenPixelDisplay | $fullScreenHeuristicDisplay | $uiMaskPixelDisplay | $uiPixelDisplay | $uiHeuristicDisplay | [$($row.rootId)]($($row.summaryPath -replace '\\','/')) |"
    $rank++
}

$markdownLines -join [Environment]::NewLine | Set-Content -Path $markdownOutPath -Encoding UTF8

$leaderboard | ConvertTo-Json -Depth 20
