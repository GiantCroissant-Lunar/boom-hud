[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPen,

    [Parameter(Mandatory = $true)]
    [string]$OutputPng,

    [string]$FontPath = "samples/UnityFullPenCompare/Assets/Resources/BoomHudFonts/PressStart2P-Regular.ttf"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function Resolve-AbsolutePath([string]$PathValue)
{
    if ([System.IO.Path]::IsPathRooted($PathValue))
    {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $PathValue))
}

function Convert-ToColor([string]$Value)
{
    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return [System.Drawing.Color]::Transparent
    }

    $hex = $Value.TrimStart('#')
    switch ($hex.Length)
    {
        6 { return [System.Drawing.Color]::FromArgb(255, [Convert]::ToInt32($hex.Substring(0, 2), 16), [Convert]::ToInt32($hex.Substring(2, 2), 16), [Convert]::ToInt32($hex.Substring(4, 2), 16)) }
        8 { return [System.Drawing.Color]::FromArgb([Convert]::ToInt32($hex.Substring(6, 2), 16), [Convert]::ToInt32($hex.Substring(0, 2), 16), [Convert]::ToInt32($hex.Substring(2, 2), 16), [Convert]::ToInt32($hex.Substring(4, 2), 16)) }
        default { throw "Unsupported color '$Value'." }
    }
}

function Get-PaddingValue($Value)
{
    if ($null -eq $Value)
    {
        return @{ Left = 0; Top = 0; Right = 0; Bottom = 0 }
    }

    if ($Value -is [System.Array])
    {
        if ($Value.Count -eq 4)
        {
            return @{ Left = [single]$Value[0]; Top = [single]$Value[1]; Right = [single]$Value[2]; Bottom = [single]$Value[3] }
        }
    }

    $padding = [single]$Value
    return @{ Left = $padding; Top = $padding; Right = $padding; Bottom = $padding }
}

function Resolve-SizeValue($Value, [single]$Available)
{
    if ($null -eq $Value)
    {
        return 0
    }

    if ($Value -is [string] -and $Value -eq "fill_container")
    {
        return $Available
    }

    return [single]$Value
}

function Get-LayoutMode($Node)
{
    if ($Node.PSObject.Properties["layout"] -and -not [string]::IsNullOrWhiteSpace([string]$Node.layout))
    {
        return [string]$Node.layout
    }

    if ($Node.PSObject.Properties["gap"] -and $null -ne $Node.gap)
    {
        return "horizontal"
    }

    return "none"
}

$fontCollection = New-Object System.Drawing.Text.PrivateFontCollection
$resolvedFontPath = Resolve-AbsolutePath $FontPath
if (Test-Path $resolvedFontPath)
{
    $fontCollection.AddFontFile($resolvedFontPath)
}

$fontFamily = if ($fontCollection.Families.Length -gt 0) { $fontCollection.Families[0] } else { [System.Drawing.FontFamily]::GenericSansSerif }

function New-Font([single]$Size)
{
    return New-Object System.Drawing.Font($fontFamily, $Size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}

function Measure-TextSize([System.Drawing.Graphics]$Graphics, $Node, [single]$AvailableWidth)
{
    $fontSize = if ($Node.PSObject.Properties["fontSize"]) { [single]$Node.fontSize } else { [single]14 }
    $font = New-Font $fontSize
    try
    {
        $content = if ($Node.PSObject.Properties["content"]) { [string]$Node.content } else { "" }
        $width = if ($Node.PSObject.Properties["width"]) { Resolve-SizeValue $Node.width $AvailableWidth } else { $AvailableWidth }
        if ($width -le 0)
        {
            $width = [Math]::Max([single]32, $AvailableWidth)
        }

        $measured = $Graphics.MeasureString($content, $font, [int][Math]::Ceiling($width))
        return [pscustomobject]@{
            Width = [single]([Math]::Ceiling($measured.Width))
            Height = [single]([Math]::Ceiling($measured.Height))
        }
    }
    finally
    {
        $font.Dispose()
    }
}

function Get-ChildDesiredSize([System.Drawing.Graphics]$Graphics, $Child, [single]$AvailableWidth, [single]$AvailableHeight)
{
    if ($Child.type -eq "text")
    {
        return Measure-TextSize -Graphics $Graphics -Node $Child -AvailableWidth $AvailableWidth
    }

    $width = if ($Child.PSObject.Properties["width"]) { Resolve-SizeValue $Child.width $AvailableWidth } else { [single]0 }
    $height = if ($Child.PSObject.Properties["height"]) { Resolve-SizeValue $Child.height $AvailableHeight } else { [single]0 }

    return [pscustomobject]@{
        Width = $width
        Height = $height
    }
}

function Draw-Node([System.Drawing.Graphics]$Graphics, $Node, [single]$OriginX, [single]$OriginY, [single]$AvailableWidth, [single]$AvailableHeight)
{
    $width = if ($Node.PSObject.Properties["width"]) { Resolve-SizeValue $Node.width $AvailableWidth } else { $AvailableWidth }
    $height = if ($Node.PSObject.Properties["height"]) { Resolve-SizeValue $Node.height $AvailableHeight } else { $AvailableHeight }
    $x = if ($Node.PSObject.Properties["x"]) { $OriginX + [single]$Node.x } else { $OriginX }
    $y = if ($Node.PSObject.Properties["y"]) { $OriginY + [single]$Node.y } else { $OriginY }
    $rect = New-Object System.Drawing.RectangleF($x, $y, $width, $height)

    if ($Node.type -ne "text" -and $Node.type -ne "icon_font")
    {
        $fillColor = if ($Node.PSObject.Properties["fill"]) { Convert-ToColor $Node.fill } else { [System.Drawing.Color]::Transparent }
        if ($fillColor.A -gt 0)
        {
            $brush = New-Object System.Drawing.SolidBrush($fillColor)
            try
            {
                if ($Node.type -eq "ellipse")
                {
                    $Graphics.FillEllipse($brush, $rect)
                }
                else
                {
                    $Graphics.FillRectangle($brush, $rect)
                }
            }
            finally
            {
                $brush.Dispose()
            }
        }

        if ($Node.PSObject.Properties["stroke"] -and $null -ne $Node.stroke)
        {
            $strokeColor = Convert-ToColor ([string]$Node.stroke.fill)
            $strokeThickness = [single]$Node.stroke.thickness
            $pen = New-Object System.Drawing.Pen($strokeColor, $strokeThickness)
            try
            {
                if ($Node.type -eq "ellipse")
                {
                    $Graphics.DrawEllipse($pen, $rect)
                }
                else
                {
                    $Graphics.DrawRectangle($pen, $rect.X, $rect.Y, $rect.Width, $rect.Height)
                }
            }
            finally
            {
                $pen.Dispose()
            }
        }
    }

    if ($Node.type -eq "text")
    {
        $fontSize = if ($Node.PSObject.Properties["fontSize"]) { [single]$Node.fontSize } else { [single]14 }
        $font = New-Font $fontSize
        $brush = New-Object System.Drawing.SolidBrush((Convert-ToColor $Node.fill))
        try
        {
            $format = New-Object System.Drawing.StringFormat
            $format.Trimming = [System.Drawing.StringTrimming]::Word
            $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoClip
            $Graphics.DrawString([string]$Node.content, $font, $brush, $rect, $format)
        }
        finally
        {
            $brush.Dispose()
            $font.Dispose()
        }
        return
    }

    if ($Node.type -eq "icon_font")
    {
        $fontSize = [Math]::Min($rect.Width, $rect.Height) * [single]0.55
        $font = New-Font $fontSize
        $brush = New-Object System.Drawing.SolidBrush((Convert-ToColor $Node.fill))
        try
        {
            $fallbackLabel = if ($Node.PSObject.Properties["iconFontName"]) { [string]$Node.iconFontName } else { "*" }
            $label = if ($fallbackLabel.Length -gt 2) { $fallbackLabel.Substring(0, 2).ToUpperInvariant() } else { $fallbackLabel.ToUpperInvariant() }
            $format = New-Object System.Drawing.StringFormat
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            $Graphics.DrawString($label, $font, $brush, $rect, $format)
        }
        finally
        {
            $brush.Dispose()
            $font.Dispose()
        }
        return
    }

    if (-not $Node.PSObject.Properties["children"] -or $null -eq $Node.children)
    {
        return
    }

    $paddingValue = if ($Node.PSObject.Properties["padding"]) { $Node.padding } else { $null }
    $padding = Get-PaddingValue $paddingValue
    $contentX = $rect.X + $padding.Left
    $contentY = $rect.Y + $padding.Top
    $contentWidth = [Math]::Max([single]0, $rect.Width - $padding.Left - $padding.Right)
    $contentHeight = [Math]::Max([single]0, $rect.Height - $padding.Top - $padding.Bottom)
    $layoutMode = Get-LayoutMode $Node
    $gap = if ($Node.PSObject.Properties["gap"]) { [single]$Node.gap } else { [single]0 }
    $children = @($Node.children)

    if ($layoutMode -eq "none")
    {
        foreach ($child in $children)
        {
            Draw-Node -Graphics $Graphics -Node $child -OriginX $rect.X -OriginY $rect.Y -AvailableWidth $contentWidth -AvailableHeight $contentHeight
        }
        return
    }

    $desired = @()
    foreach ($child in $children)
    {
        $desired += Get-ChildDesiredSize -Graphics $Graphics -Child $child -AvailableWidth $contentWidth -AvailableHeight $contentHeight
    }

    $mainAxisTotal = [single]0
    for ($index = 0; $index -lt $desired.Count; $index++)
    {
        $mainAxisTotal += if ($layoutMode -eq "vertical") { $desired[$index].Height } else { $desired[$index].Width }
        if ($index -lt ($desired.Count - 1))
        {
            $mainAxisTotal += $gap
        }
    }

    $startMain = if ($layoutMode -eq "vertical") { $contentY } else { $contentX }
    if ($Node.PSObject.Properties["justifyContent"] -and [string]$Node.justifyContent -eq "center")
    {
        $availableMain = if ($layoutMode -eq "vertical") { $contentHeight } else { $contentWidth }
        $startMain += [Math]::Max([single]0, ($availableMain - $mainAxisTotal) / [single]2)
    }

    $cursor = $startMain
    for ($index = 0; $index -lt $children.Count; $index++)
    {
        $child = $children[$index]
        $size = $desired[$index]

        if ($layoutMode -eq "vertical")
        {
            $childX = $contentX
            if ($Node.PSObject.Properties["alignItems"] -and [string]$Node.alignItems -eq "center")
            {
                $childX += [Math]::Max([single]0, ($contentWidth - $size.Width) / [single]2)
            }

            Draw-Node -Graphics $Graphics -Node $child -OriginX $childX -OriginY $cursor -AvailableWidth $contentWidth -AvailableHeight $size.Height
            $cursor += $size.Height + $gap
        }
        else
        {
            $childY = $contentY
            if ($Node.PSObject.Properties["alignItems"] -and [string]$Node.alignItems -eq "center")
            {
                $childY += [Math]::Max([single]0, ($contentHeight - $size.Height) / [single]2)
            }

            Draw-Node -Graphics $Graphics -Node $child -OriginX $cursor -OriginY $childY -AvailableWidth $size.Width -AvailableHeight $contentHeight
            $cursor += $size.Width + $gap
        }
    }
}

$inputPath = Resolve-AbsolutePath $InputPen
$outputPath = Resolve-AbsolutePath $OutputPng
$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path $outputDirectory))
{
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$document = Get-Content $inputPath -Raw | ConvertFrom-Json
$root = @($document.children)[0]
$bitmap = New-Object System.Drawing.Bitmap([int]$root.width, [int]$root.height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try
{
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::Transparent)
    Draw-Node -Graphics $graphics -Node $root -OriginX ([single]0) -OriginY ([single]0) -AvailableWidth ([single]$root.width) -AvailableHeight ([single]$root.height)
    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally
{
    $graphics.Dispose()
    $bitmap.Dispose()
}
