[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPen,

    [Parameter(Mandatory = $true)]
    [string]$ReferenceImage,

    [string]$OutputDir = "",

    [string]$ReferenceUrl = "",

    [string]$UiMaskManifest = "",

    [string]$MaskKey = "",

    [string]$Normalize = "stretch",

    [int]$Tolerance = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

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

function Invoke-ExternalCommand([string]$WorkingDirectory, [string]$FilePath, [string[]]$Arguments)
{
    Push-Location $WorkingDirectory
    try
    {
        & $FilePath @Arguments
        $exitCodeVariable = Get-Variable -Name LASTEXITCODE -ErrorAction SilentlyContinue
        $exitCode = if ($null -ne $exitCodeVariable) { [int]$exitCodeVariable.Value } else { 0 }
        if ($exitCode -ne 0)
        {
            throw "Command '$FilePath $($Arguments -join ' ')' failed with exit code $exitCode."
        }
    }
    finally
    {
        Pop-Location
    }
}

function Invoke-BaselineScore(
    [string]$RepoRoot,
    [string]$CliProject,
    [string]$Reference,
    [string]$Candidate,
    [string]$OutPath,
    [string]$DiffPath,
    [string]$NormalizeMode,
    [int]$ColorTolerance)
{
    Invoke-ExternalCommand -WorkingDirectory $RepoRoot -FilePath "dotnet" -Arguments @(
        "run",
        "-c",
        "Release",
        "--project",
        $CliProject,
        "--",
        "baseline",
        "score",
        "--reference",
        $Reference,
        "--candidate",
        $Candidate,
        "--out",
        $OutPath,
        "--diff",
        $DiffPath,
        "--normalize",
        $NormalizeMode,
        "--tolerance",
        $ColorTolerance.ToString(),
        "--summary",
        "false"
    )
}

function Convert-ToRectangle([object]$Rect)
{
    return New-Object System.Drawing.Rectangle(
        [int]$Rect.x,
        [int]$Rect.y,
        [int]$Rect.width,
        [int]$Rect.height)
}

function Write-MaskedImage(
    [string]$InputImagePath,
    [string]$OutputImagePath,
    [object[]]$MaskRects)
{
    $source = [System.Drawing.Bitmap]::FromFile($InputImagePath)
    try
    {
        $masked = New-Object System.Drawing.Bitmap($source.Width, $source.Height)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($masked)
            try
            {
                $graphics.Clear([System.Drawing.Color]::Black)
                foreach ($maskRect in $MaskRects)
                {
                    $rect = Convert-ToRectangle $maskRect
                    $graphics.DrawImage($source, $rect, $rect, [System.Drawing.GraphicsUnit]::Pixel)
                }
            }
            finally
            {
                $graphics.Dispose()
            }

            $masked.Save($OutputImagePath)
        }
        finally
        {
            $masked.Dispose()
        }
    }
    finally
    {
        $source.Dispose()
    }
}

function Get-UiMaskRects(
    [string]$ManifestPath,
    [string]$FixtureKey)
{
    if ([string]::IsNullOrWhiteSpace($ManifestPath))
    {
        return $null
    }

    $resolvedManifest = Resolve-AbsolutePath $ManifestPath
    if (-not (Test-Path $resolvedManifest))
    {
        throw "UI mask manifest not found: $resolvedManifest"
    }

    $manifest = Get-Content $resolvedManifest -Raw | ConvertFrom-Json -Depth 20
    foreach ($fixture in @($manifest.fixtures))
    {
        $candidateKeys = @()
        if ($fixture.PSObject.Properties["fixtureSlug"]) { $candidateKeys += [string]$fixture.fixtureSlug }
        if ($fixture.PSObject.Properties["rootId"]) { $candidateKeys += [string]$fixture.rootId }
        if ($fixture.PSObject.Properties["key"]) { $candidateKeys += [string]$fixture.key }
        if ($candidateKeys -contains $FixtureKey)
        {
            return @($fixture.uiMaskRects)
        }
    }

    return $null
}

$repoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
$inputPath = Resolve-AbsolutePath $InputPen
$referencePath = Resolve-AbsolutePath $ReferenceImage

if (-not (Test-Path $inputPath))
{
    throw "Input pen file not found: $inputPath"
}

if (-not (Test-Path $referencePath))
{
    throw "Reference image not found: $referencePath"
}

$document = Get-Content $inputPath -Raw | ConvertFrom-Json
$root = @($document.children)[0]
$rootId = if ($root.PSObject.Properties["id"]) { [string]$root.id } else { [System.IO.Path]::GetFileNameWithoutExtension($inputPath) }
$fixtureSlug = [System.IO.Path]::GetFileNameWithoutExtension($inputPath)

$resolvedOutputDir =
    if ([string]::IsNullOrWhiteSpace($OutputDir))
    {
        Join-Path $repoRoot (Join-Path "build\\fixture-refs" $fixtureSlug)
    }
    else
    {
        Resolve-AbsolutePath $OutputDir
    }

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

$renderedPenPath = Join-Path $resolvedOutputDir "$rootId.pen-render.png"
$scorePath = Join-Path $resolvedOutputDir "$rootId.reference-score.json"
$diffPath = Join-Path $resolvedOutputDir "$rootId.reference-diff.png"
$uiScorePath = Join-Path $resolvedOutputDir "$rootId.reference-ui-score.json"
$uiDiffPath = Join-Path $resolvedOutputDir "$rootId.reference-ui-diff.png"
$uiReferenceMaskPath = Join-Path $resolvedOutputDir "$rootId.reference-ui-source.png"
$uiPenMaskPath = Join-Path $resolvedOutputDir "$rootId.reference-ui-pen.png"
$summaryPath = Join-Path $resolvedOutputDir "$rootId.reference-summary.json"

$renderScript = Join-Path $repoRoot "scripts\\render-pen-fixture-ref.ps1"
& $renderScript -InputPen $inputPath -OutputPng $renderedPenPath

$cliProject = Join-Path $repoRoot "dotnet\\src\\BoomHud.Cli\\BoomHud.Cli.csproj"
Invoke-BaselineScore -RepoRoot $repoRoot -CliProject $cliProject -Reference $referencePath -Candidate $renderedPenPath -OutPath $scorePath -DiffPath $diffPath -NormalizeMode $Normalize -ColorTolerance $Tolerance

$scoreReport = Get-Content $scorePath -Raw | ConvertFrom-Json
$fixtureKey = if (-not [string]::IsNullOrWhiteSpace($MaskKey)) { $MaskKey } else { $fixtureSlug }
$uiMaskRects = Get-UiMaskRects -ManifestPath $UiMaskManifest -FixtureKey $fixtureKey
$uiScoreReport = $null

if ($null -ne $uiMaskRects -and $uiMaskRects.Count -gt 0)
{
    Write-MaskedImage -InputImagePath $referencePath -OutputImagePath $uiReferenceMaskPath -MaskRects $uiMaskRects
    Write-MaskedImage -InputImagePath $renderedPenPath -OutputImagePath $uiPenMaskPath -MaskRects $uiMaskRects
    Invoke-BaselineScore -RepoRoot $repoRoot -CliProject $cliProject -Reference $uiReferenceMaskPath -Candidate $uiPenMaskPath -OutPath $uiScorePath -DiffPath $uiDiffPath -NormalizeMode $Normalize -ColorTolerance $Tolerance
    $uiScoreReport = Get-Content $uiScorePath -Raw | ConvertFrom-Json
}

$summary = [pscustomobject]@{
    inputPen = $inputPath
    rootId = $rootId
    rootName = if ($root.PSObject.Properties["name"]) { [string]$root.name } else { $fixtureSlug }
    referenceImage = $referencePath
    referenceUrl = $ReferenceUrl
    renderedPen = $renderedPenPath
    normalize = $Normalize
    tolerance = $Tolerance
    primaryReferenceSimilarity = if ($null -ne $uiScoreReport)
    {
        [pscustomobject]@{
            mode = "uiOnly"
            overallSimilarityPercent = [double]$uiScoreReport.OverallSimilarityPercent
        }
    }
    else
    {
        [pscustomobject]@{
            mode = "fullScreen"
            overallSimilarityPercent = [double]$scoreReport.OverallSimilarityPercent
        }
    }
    fullScreen = [pscustomobject]@{
        scoreReport = $scorePath
        diffImage = $diffPath
        pixelIdentityPercent = [double]$scoreReport.PixelIdentityPercent
        deltaSimilarityPercent = [double]$scoreReport.DeltaSimilarityPercent
        overallSimilarityPercent = [double]$scoreReport.OverallSimilarityPercent
    }
    uiOnly = if ($null -ne $uiScoreReport)
    {
        [pscustomobject]@{
            maskKey = $fixtureKey
            maskRects = $uiMaskRects
            maskedReferenceImage = $uiReferenceMaskPath
            maskedRenderedPen = $uiPenMaskPath
            scoreReport = $uiScorePath
            diffImage = $uiDiffPath
            pixelIdentityPercent = [double]$uiScoreReport.PixelIdentityPercent
            deltaSimilarityPercent = [double]$uiScoreReport.DeltaSimilarityPercent
            overallSimilarityPercent = [double]$uiScoreReport.OverallSimilarityPercent
        }
    }
    else
    {
        $null
    }
}

$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 10
