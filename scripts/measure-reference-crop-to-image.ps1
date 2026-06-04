[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReferenceImage,

    [Parameter(Mandatory = $true)]
    [int]$CropX,

    [Parameter(Mandatory = $true)]
    [int]$CropY,

    [Parameter(Mandatory = $true)]
    [int]$CropWidth,

    [Parameter(Mandatory = $true)]
    [int]$CropHeight,

    [Parameter(Mandatory = $true)]
    [string]$CandidateImage,

    [string]$OutputDir = "",

    [ValidateSet("stretch")]
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

function Write-CroppedImage(
    [string]$InputImagePath,
    [string]$OutputImagePath,
    [int]$X,
    [int]$Y,
    [int]$Width,
    [int]$Height)
{
    $source = [System.Drawing.Bitmap]::FromFile($InputImagePath)
    try
    {
        $cropRect = New-Object System.Drawing.Rectangle($X, $Y, $Width, $Height)
        $cropped = New-Object System.Drawing.Bitmap($cropRect.Width, $cropRect.Height)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($cropped)
            try
            {
                $graphics.DrawImage($source, (New-Object System.Drawing.Rectangle(0, 0, $cropRect.Width, $cropRect.Height)), $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally
            {
                $graphics.Dispose()
            }

            $cropped.Save($OutputImagePath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $cropped.Dispose()
        }
    }
    finally
    {
        $source.Dispose()
    }
}

function Write-StretchedImage(
    [string]$InputImagePath,
    [string]$OutputImagePath,
    [int]$Width,
    [int]$Height)
{
    $source = [System.Drawing.Bitmap]::FromFile($InputImagePath)
    try
    {
        $scaled = New-Object System.Drawing.Bitmap($Width, $Height)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($scaled)
            try
            {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($source, (New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)))
            }
            finally
            {
                $graphics.Dispose()
            }

            $scaled.Save($OutputImagePath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $scaled.Dispose()
        }
    }
    finally
    {
        $source.Dispose()
    }
}

function Write-CompositedImage(
    [string]$BackgroundImagePath,
    [string]$ForegroundImagePath,
    [string]$OutputImagePath)
{
    $background = [System.Drawing.Bitmap]::FromFile($BackgroundImagePath)
    $foreground = [System.Drawing.Bitmap]::FromFile($ForegroundImagePath)
    try
    {
        if ($background.Width -ne $foreground.Width -or $background.Height -ne $foreground.Height)
        {
            throw "Background and foreground images must have identical dimensions for compositing."
        }

        $composited = New-Object System.Drawing.Bitmap($background.Width, $background.Height)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($composited)
            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.DrawImage($background, 0, 0, $background.Width, $background.Height)
                $graphics.DrawImage($foreground, 0, 0, $foreground.Width, $foreground.Height)
            }
            finally
            {
                $graphics.Dispose()
            }

            $composited.Save($OutputImagePath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $composited.Dispose()
        }
    }
    finally
    {
        $background.Dispose()
        $foreground.Dispose()
    }
}

function Get-RgbaMetrics(
    [string]$ReferenceImagePath,
    [string]$CandidateImagePath,
    [int]$ColorTolerance)
{
    $reference = [System.Drawing.Bitmap]::FromFile($ReferenceImagePath)
    $candidate = [System.Drawing.Bitmap]::FromFile($CandidateImagePath)
    try
    {
        if ($reference.Width -ne $candidate.Width -or $reference.Height -ne $candidate.Height)
        {
            throw "Reference and candidate images must have identical dimensions for RGBA comparison."
        }

        $totalPixels = [int64]$reference.Width * [int64]$reference.Height
        $changedPixels = 0L
        $exactMatchedPixels = 0L
        $exactMatchedChannels = 0L
        $toleranceMatchedChannels = 0L

        for ($y = 0; $y -lt $reference.Height; $y++)
        {
            for ($x = 0; $x -lt $reference.Width; $x++)
            {
                $referencePixel = $reference.GetPixel($x, $y)
                $candidatePixel = $candidate.GetPixel($x, $y)
                $channelDeltas = @(
                    [Math]::Abs([int]$referencePixel.R - [int]$candidatePixel.R),
                    [Math]::Abs([int]$referencePixel.G - [int]$candidatePixel.G),
                    [Math]::Abs([int]$referencePixel.B - [int]$candidatePixel.B),
                    [Math]::Abs([int]$referencePixel.A - [int]$candidatePixel.A)
                )

                $isExactPixelMatch = $true
                $isTolerancePixelMatch = $true
                foreach ($channelDelta in $channelDeltas)
                {
                    if ($channelDelta -eq 0)
                    {
                        $exactMatchedChannels++
                    }
                    else
                    {
                        $isExactPixelMatch = $false
                    }

                    if ($channelDelta -le $ColorTolerance)
                    {
                        $toleranceMatchedChannels++
                    }
                    else
                    {
                        $isTolerancePixelMatch = $false
                    }
                }

                if ($isExactPixelMatch)
                {
                    $exactMatchedPixels++
                }

                if (-not $isTolerancePixelMatch)
                {
                    $changedPixels++
                }
            }
        }

        $totalChannels = $totalPixels * 4L

        return [pscustomobject]@{
            totalPixels = $totalPixels
            changedPixels = $changedPixels
            tolerancePixelIdentityPercent = [Math]::Round((1.0 - ($changedPixels / [double]$totalPixels)) * 100.0, 4)
            exactMatchedPixels = $exactMatchedPixels
            exactPixelIdentityPercent = [Math]::Round(($exactMatchedPixels / [double]$totalPixels) * 100.0, 4)
            totalChannels = $totalChannels
            exactMatchedChannels = $exactMatchedChannels
            toleranceMatchedChannels = $toleranceMatchedChannels
            exactChannelIdentityPercent = [Math]::Round(($exactMatchedChannels / [double]$totalChannels) * 100.0, 4)
            toleranceChannelIdentityPercent = [Math]::Round(($toleranceMatchedChannels / [double]$totalChannels) * 100.0, 4)
        }
    }
    finally
    {
        $reference.Dispose()
        $candidate.Dispose()
    }
}

$repoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
$referencePath = Resolve-AbsolutePath $ReferenceImage
$candidatePath = Resolve-AbsolutePath $CandidateImage

if (-not (Test-Path $referencePath))
{
    throw "Reference image not found: $referencePath"
}

if (-not (Test-Path $candidatePath))
{
    throw "Candidate image not found: $candidatePath"
}

$resolvedOutputDir =
    if ([string]::IsNullOrWhiteSpace($OutputDir))
    {
        Join-Path $repoRoot "build/component-crop-score"
    }
    else
    {
        Resolve-AbsolutePath $OutputDir
    }

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

$referenceCropPath = Join-Path $resolvedOutputDir "reference-crop.png"
$candidateNormalizedPath = Join-Path $resolvedOutputDir "candidate-normalized.png"
$candidateCompositedPath = Join-Path $resolvedOutputDir "candidate-composited.png"
$scorePath = Join-Path $resolvedOutputDir "image-score.json"
$diffPath = Join-Path $resolvedOutputDir "image-diff.png"
$summaryPath = Join-Path $resolvedOutputDir "image-summary.json"

Write-CroppedImage -InputImagePath $referencePath -OutputImagePath $referenceCropPath -X $CropX -Y $CropY -Width $CropWidth -Height $CropHeight
Write-StretchedImage -InputImagePath $candidatePath -OutputImagePath $candidateNormalizedPath -Width $CropWidth -Height $CropHeight
Write-CompositedImage -BackgroundImagePath $referenceCropPath -ForegroundImagePath $candidateNormalizedPath -OutputImagePath $candidateCompositedPath

$cliProject = Join-Path $repoRoot "dotnet\\src\\BoomHud.Cli\\BoomHud.Cli.csproj"
Invoke-ExternalCommand -WorkingDirectory $repoRoot -FilePath "dotnet" -Arguments @(
    "run",
    "-c",
    "Release",
    "--project",
    $cliProject,
    "--",
    "baseline",
    "score",
    "--reference",
    $referenceCropPath,
    "--candidate",
    $candidateCompositedPath,
    "--out",
    $scorePath,
    "--diff",
    $diffPath,
    "--normalize",
    $Normalize,
    "--tolerance",
    $Tolerance.ToString(),
    "--summary",
    "false"
)

$scoreReport = Get-Content $scorePath -Raw | ConvertFrom-Json
$rgbaMetrics = Get-RgbaMetrics -ReferenceImagePath $referenceCropPath -CandidateImagePath $candidateCompositedPath -ColorTolerance $Tolerance

$summary = [pscustomobject]@{
    referenceImage = $referencePath
    referenceCrop = [pscustomobject]@{
        x = $CropX
        y = $CropY
        width = $CropWidth
        height = $CropHeight
        image = $referenceCropPath
    }
    candidateImage = $candidatePath
    normalizedCandidateImage = $candidateNormalizedPath
    compositedCandidateImage = $candidateCompositedPath
    tolerance = $Tolerance
    normalize = $Normalize
    rgba = $rgbaMetrics
    baseline = [pscustomobject]@{
        scoreReport = $scorePath
        diffImage = $diffPath
        pixelIdentityPercent = [double]$scoreReport.PixelIdentityPercent
        deltaSimilarityPercent = [double]$scoreReport.DeltaSimilarityPercent
        overallSimilarityPercent = [double]$scoreReport.OverallSimilarityPercent
    }
}

$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 10
