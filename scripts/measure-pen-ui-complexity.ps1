[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPen,

    [int]$SampleWidth = 480,

    [string]$OutputJson = ""
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

function Resolve-SizeValue($Value, [single]$Available)
{
    if ($null -eq $Value)
    {
        return [single]0
    }

    if ($Value -is [string] -and $Value -eq "fill_container")
    {
        return $Available
    }

    return [single]$Value
}

function Get-BackgroundLike([object]$Node, [single]$RootWidth, [single]$RootHeight, [int]$Depth)
{
    if ($Depth -gt 1)
    {
        return $false
    }

    $name = if ($Node.PSObject.Properties["name"]) { [string]$Node.name } else { "" }
    $width = if ($Node.PSObject.Properties["width"]) { [single]$Node.width } else { [single]0 }
    $height = if ($Node.PSObject.Properties["height"]) { [single]$Node.height } else { [single]0 }
    $coversScreen = $width -ge ($RootWidth * [single]0.9) -and $height -ge ($RootHeight * [single]0.9)
    $hasStroke = $Node.PSObject.Properties["stroke"] -and $null -ne $Node.stroke
    return $coversScreen -and -not $hasStroke -and $name -match '(?i)backdrop|background|tint|shade|wash'
}

function Draw-CoverageNode([System.Drawing.Graphics]$Graphics, $Node, [single]$OriginX, [single]$OriginY, [single]$AvailableWidth, [single]$AvailableHeight)
{
    $width = if ($Node.PSObject.Properties["width"]) { Resolve-SizeValue $Node.width $AvailableWidth } else { $AvailableWidth }
    $height = if ($Node.PSObject.Properties["height"]) { Resolve-SizeValue $Node.height $AvailableHeight } else { $AvailableHeight }
    $x = if ($Node.PSObject.Properties["x"]) { $OriginX + [single]$Node.x } else { $OriginX }
    $y = if ($Node.PSObject.Properties["y"]) { $OriginY + [single]$Node.y } else { $OriginY }

    if ($Node.type -eq "text")
    {
        $fontSize = if ($Node.PSObject.Properties["fontSize"]) { [single]$Node.fontSize } else { [single]14 }
        $content = if ($Node.PSObject.Properties["content"]) { [string]$Node.content } else { "" }
        if (-not $Node.PSObject.Properties["width"])
        {
            $estimatedWidth = [Math]::Max([single]8, [single]($content.Length * $fontSize * [single]0.62))
            $width = [Math]::Min($estimatedWidth, [Math]::Max([single]8, $AvailableWidth))
        }
        if (-not $Node.PSObject.Properties["height"])
        {
            $height = [single]([Math]::Ceiling($fontSize * [single]1.5))
        }
    }

    $rect = New-Object System.Drawing.RectangleF($x, $y, $width, $height)

    $hasPaint = $Node.PSObject.Properties["fill"] -or ($Node.PSObject.Properties["stroke"] -and $null -ne $Node.stroke)
    if ($Node.type -eq "text" -or $Node.type -eq "icon_font" -or $hasPaint)
    {
        $brush = [System.Drawing.Brushes]::White
        switch ([string]$Node.type)
        {
            "ellipse" { $Graphics.FillEllipse($brush, $rect) }
            "text" { $Graphics.FillRectangle($brush, $rect) }
            "icon_font" { $Graphics.FillEllipse($brush, $rect) }
            default { $Graphics.FillRectangle($brush, $rect) }
        }
    }

    if (-not $Node.PSObject.Properties["children"] -or $null -eq $Node.children)
    {
        return
    }

    foreach ($child in @($Node.children))
    {
        Draw-CoverageNode -Graphics $Graphics -Node $child -OriginX $rect.X -OriginY $rect.Y -AvailableWidth $rect.Width -AvailableHeight $rect.Height
    }
}

$inputPath = Resolve-AbsolutePath $InputPen
$document = Get-Content $inputPath -Raw | ConvertFrom-Json
$root = @($document.children)[0]
$rootWidth = [single]$root.width
$rootHeight = [single]$root.height
$scale = [single]$SampleWidth / $rootWidth
$sampleHeight = [int][Math]::Ceiling($rootHeight * $scale)

$bitmap = New-Object System.Drawing.Bitmap($SampleWidth, $sampleHeight)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try
{
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform($scale, $scale)

    $queue = New-Object System.Collections.Generic.Queue[object]
    foreach ($child in @($root.children))
    {
        $queue.Enqueue([pscustomobject]@{ Node = $child; Depth = 1 })
    }

    $countAll = 0
    $countVisual = 0
    $countText = 0
    $countIcons = 0
    $countContainers = 0

    while ($queue.Count -gt 0)
    {
        $entry = $queue.Dequeue()
        $node = $entry.Node
        $depth = [int]$entry.Depth
        $countAll++

        $isBackground = Get-BackgroundLike -Node $node -RootWidth $rootWidth -RootHeight $rootHeight -Depth $depth
        if (-not $isBackground)
        {
            Draw-CoverageNode -Graphics $graphics -Node $node -OriginX ([single]0) -OriginY ([single]0) -AvailableWidth $rootWidth -AvailableHeight $rootHeight
            $countVisual++
            if ($node.type -eq "text") { $countText++ }
            if ($node.type -eq "icon_font") { $countIcons++ }
            if ($node.type -eq "frame" -or $node.type -eq "group") { $countContainers++ }
        }

        if ($node.PSObject.Properties["children"] -and $null -ne $node.children)
        {
            foreach ($child in @($node.children))
            {
                $queue.Enqueue([pscustomobject]@{ Node = $child; Depth = $depth + 1 })
            }
        }
    }

    $filled = 0
    for ($y = 0; $y -lt $bitmap.Height; $y++)
    {
        for ($x = 0; $x -lt $bitmap.Width; $x++)
        {
            if ($bitmap.GetPixel($x, $y).A -gt 0)
            {
                $filled++
            }
        }
    }

    $total = $bitmap.Width * $bitmap.Height
    $coveragePct = [Math]::Round(($filled / [double]$total) * 100.0, 2)
    $nodeDensity = [Math]::Round(($countVisual / [double]([Math]::Max(1, ($rootWidth * $rootHeight / 100000.0)))), 2)

    $coverageClassification =
        if ($coveragePct -lt 8) { "sparse" }
        elseif ($coveragePct -lt 18) { "medium" }
        else { "dense" }

    $complexityBand =
        if ($coveragePct -lt 12 -and $nodeDensity -lt 1.6 -and $countIcons -lt 6 -and $countVisual -lt 24) { "low" }
        elseif ($coveragePct -lt 35 -and $nodeDensity -lt 2.6 -and $countVisual -lt 42) { "medium" }
        else { "high" }

    $result = [pscustomobject]@{
        input = $inputPath
        rootName = [string]$root.name
        screen = [pscustomobject]@{
            width = [int]$root.width
            height = [int]$root.height
        }
        uiCoveragePct = $coveragePct
        uiPixelCount = $filled
        samplePixelCount = $total
        visualNodeCount = $countVisual
        totalNodeCount = $countAll
        textNodeCount = $countText
        iconNodeCount = $countIcons
        containerNodeCount = $countContainers
        nodeDensityPer100kPx = $nodeDensity
        coverageClassification = $coverageClassification
        complexityBand = $complexityBand
        notes = @(
            "coverage is only one axis; radial or modal UIs can be low-coverage but still structurally demanding",
            "background-like near-fullscreen layers are excluded from coverage"
        )
    }

    $json = $result | ConvertTo-Json -Depth 8
    if (-not [string]::IsNullOrWhiteSpace($OutputJson))
    {
        $outputPath = Resolve-AbsolutePath $OutputJson
        $outputDir = Split-Path -Parent $outputPath
        if (-not (Test-Path $outputDir))
        {
            New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
        }
        Set-Content -LiteralPath $outputPath -Value $json
    }

    Write-Output $json
}
finally
{
    $graphics.Dispose()
    $bitmap.Dispose()
}
