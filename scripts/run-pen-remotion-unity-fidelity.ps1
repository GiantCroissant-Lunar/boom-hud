[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$ManifestPath = "fidelity\pen-remotion-unity.fullpen.json",
    [string]$RulesPath = "",
    [string]$UnityProjectPath = "",
    [string]$UnityExe = "",
    [switch]$SkipDotNet,
    [switch]$SkipRemotion,
    [switch]$SkipUnityCapture,
    [switch]$SkipScoring
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section([string]$Message)
{
    Write-Host ""
    Write-Host "==> $Message"
}

function Resolve-AbsolutePath([string]$PathValue)
{
    if ([System.IO.Path]::IsPathRooted($PathValue))
    {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $PathValue))
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

function Wait-ForUnityProjectAvailability(
    [string]$ProjectPath,
    [int]$TimeoutSeconds = 60)
{
    if ([string]::IsNullOrWhiteSpace($ProjectPath))
    {
        return
    }

    $projectPath = [System.IO.Path]::GetFullPath($ProjectPath)
    $projectPathPattern = [regex]::Escape($projectPath)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline)
    {
        $unityProcesses = @(
            Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.Name -eq "Unity.exe" -and
                    -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
                    $_.CommandLine -match $projectPathPattern
                }
        )

        if ($unityProcesses.Count -eq 0)
        {
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for Unity project '$projectPath' to become available."
}

function Invoke-DotNetCli([string[]]$Arguments)
{
    Invoke-ExternalCommand -WorkingDirectory $RepoRoot -FilePath "dotnet" -Arguments $Arguments
}

function Normalize-ArtifactStem([string]$Value)
{
    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return ""
    }

    $trimmed = $Value -replace '\.visual-ir$', ''
    $trimmed = $trimmed -replace '-(ugui|uitk|unity|react|remotion)$', ''
    return ([regex]::Replace($trimmed, '[^a-zA-Z0-9]', '')).ToLowerInvariant()
}

function Get-VisualIrArtifact([string]$ArtifactsDirectory, [string]$Hint)
{
    if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory) -or -not (Test-Path $ArtifactsDirectory))
    {
        return $null
    }

    $artifacts = @(
        Get-ChildItem -Path $ArtifactsDirectory -Filter *.visual-ir.json -File -ErrorAction SilentlyContinue |
            Sort-Object Name
    )

    if ($artifacts.Count -eq 0)
    {
        return $null
    }

    $normalizedHint = Normalize-ArtifactStem $Hint
    if (-not [string]::IsNullOrWhiteSpace($normalizedHint))
    {
        $matched = $artifacts |
            Where-Object {
                (Normalize-ArtifactStem ([System.IO.Path]::GetFileNameWithoutExtension($_.Name))) -eq $normalizedHint
            } |
            Select-Object -First 1
        if ($null -ne $matched)
        {
            return $matched.FullName
        }
    }

    return $artifacts[0].FullName
}

function Invoke-RemotionCommand([string[]]$Arguments)
{
    $workingDirectory = Join-Path $RepoRoot "remotion"
    $filePath = $Arguments[0]
    $remainingArguments = if ($Arguments.Length -gt 1) { $Arguments[1..($Arguments.Length - 1)] } else { @() }
    Invoke-ExternalCommand -WorkingDirectory $workingDirectory -FilePath $filePath -Arguments $remainingArguments
}

function Invoke-TrimTransparency([string]$InputPath)
{
    if (-not (Test-Path $InputPath))
    {
        return
    }

    $script = @'
import argparse
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("--input", required=True)
args = parser.parse_args()

with Image.open(args.input) as image:
    if image.mode != "RGBA":
        image = image.convert("RGBA")

    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    if bbox:
        image.crop(bbox).save(args.input)
'@

    $script | python - --input $InputPath | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "Transparent trim failed for '$InputPath'."
    }
}

function Get-MotionSequenceInfo([pscustomobject]$Timeline)
{
    $motionJsonPath = Resolve-AbsolutePath $Timeline.motionJsonPath
    $motion = Get-Content $motionJsonPath -Raw | ConvertFrom-Json -Depth 20
    $defaultSequenceId = if ([string]::IsNullOrWhiteSpace($motion.defaultSequenceId)) { $motion.sequences[0].id } else { $motion.defaultSequenceId }
    $sequence = $motion.sequences | Where-Object { $_.id -eq $defaultSequenceId } | Select-Object -First 1
    if (-not $sequence)
    {
        throw "Could not resolve default motion sequence in '$motionJsonPath'."
    }

    $clipMap = @{}
    foreach ($clip in $motion.clips)
    {
        $clipMap[$clip.id] = $clip
    }

    $items = @()
    $maxFrame = 0
    foreach ($item in $sequence.items)
    {
        $clip = $clipMap[$item.clipId]
        $durationProperty = $item.PSObject.Properties["durationFrames"]
        $startProperty = $item.PSObject.Properties["startFrame"]
        $fillModeProperty = $item.PSObject.Properties["fillMode"]

        $resolvedDuration = if ($null -ne $durationProperty -and $null -ne $durationProperty.Value) { [int]$durationProperty.Value } else { [int]$clip.durationFrames }
        $resolvedStart = if ($null -ne $startProperty -and $null -ne $startProperty.Value) { [int]$startProperty.Value } else { [int]$clip.startFrame }
        $resolvedFillMode = if ($null -eq $fillModeProperty -or [string]::IsNullOrWhiteSpace([string]$fillModeProperty.Value)) { "none" } else { [string]$fillModeProperty.Value }

        $items += [pscustomobject]@{
            clipId = [string]$item.clipId
            startFrame = $resolvedStart
            durationFrames = $resolvedDuration
            fillMode = $resolvedFillMode
        }

        $maxFrame = [Math]::Max($maxFrame, $resolvedStart + $resolvedDuration)
    }

    $computedFrames = New-Object System.Collections.Generic.HashSet[int]
    foreach ($item in $items)
    {
        [void]$computedFrames.Add([int]$item.startFrame)
        $endFrame = [Math]::Min($item.startFrame + $item.durationFrames, [Math]::Max(0, $maxFrame - 1))
        [void]$computedFrames.Add([int]$endFrame)
    }

    [void]$computedFrames.Add([Math]::Max(0, $maxFrame - 1))

    return [pscustomobject]@{
        defaultSequenceId = $defaultSequenceId
        items = $items
        sampleFrames = @($computedFrames | Sort-Object)
    }
}

function Get-UnitySequenceInfo([pscustomobject]$Timeline)
{
    $generatedMotionPath = Resolve-AbsolutePath $Timeline.unityGeneratedMotionPath
    $source = Get-Content $generatedMotionPath -Raw

    $defaultSequenceMatch = [regex]::Match($source, 'public const string DefaultSequenceId = "([^"]+)";')
    if (-not $defaultSequenceMatch.Success)
    {
        throw "Could not parse DefaultSequenceId from '$generatedMotionPath'."
    }

    $defaultSequenceId = $defaultSequenceMatch.Groups[1].Value
    $casePattern = '(?s)"' + [regex]::Escape($defaultSequenceId) + '"\s*=>\s*new\[\]\s*\{(.*?)\}\s*,\s*_ =>'
    $caseMatch = [regex]::Match($source, $casePattern)
    if (-not $caseMatch.Success)
    {
        throw "Could not parse sequence items for '$defaultSequenceId' from '$generatedMotionPath'."
    }

    $itemPattern = '(?s)new TimelineSequenceClip\s*\{\s*ClipId = "([^"]+)",\s*StartFrame = (\d+),\s*DurationFrames = (\d+),\s*FillMode = TimelineSequenceFillMode\.([A-Za-z]+)\s*\}'
    $items = @()
    foreach ($match in [regex]::Matches($caseMatch.Groups[1].Value, $itemPattern))
    {
        $durationValue = [int]$match.Groups[3].Value
        $resolvedDurationValue = if ($durationValue -gt 0) { $durationValue } else { $null }
        $items += [pscustomobject]@{
            clipId = $match.Groups[1].Value
            startFrame = [int]$match.Groups[2].Value
            durationFrames = $resolvedDurationValue
            fillMode = switch ($match.Groups[4].Value)
            {
                "HoldStart" { "holdStart" }
                "HoldEnd" { "holdEnd" }
                "HoldBoth" { "holdBoth" }
                default { "none" }
            }
        }
    }

    return [pscustomobject]@{
        defaultSequenceId = $defaultSequenceId
        items = $items
    }
}

function Test-CharPortraitMetadataParity([pscustomobject]$Timeline, [string]$ArtifactsRoot)
{
    $motionInfo = Get-MotionSequenceInfo -Timeline $Timeline
    $unityInfo = Get-UnitySequenceInfo -Timeline $Timeline
    $pairs = @()

    for ($index = 0; $index -lt $motionInfo.items.Count; $index++)
    {
        $expected = $motionInfo.items[$index]
        $actual = if ($index -lt $unityInfo.items.Count) { $unityInfo.items[$index] } else { $null }
        $actualDuration = if ($null -ne $actual -and $null -ne $actual.durationFrames) { $actual.durationFrames } else { $expected.durationFrames }
        $pairs += [pscustomobject]@{
            clipId = $expected.clipId
            startFrameMatches = ($null -ne $actual) -and ($expected.startFrame -eq $actual.startFrame)
            durationMatches = ($null -ne $actual) -and ($expected.durationFrames -eq $actualDuration)
            fillModeMatches = ($null -ne $actual) -and ($expected.fillMode -eq $actual.fillMode)
            expected = $expected
            actual = $actual
        }
    }

    $manifestSampleFrames = @($Timeline.sampleFrames | ForEach-Object { [int]$_ } | Sort-Object)
    $computedSampleFrames = @($motionInfo.sampleFrames)
    $sampleFramesMatch = ($manifestSampleFrames.Count -eq $computedSampleFrames.Count) -and (@(Compare-Object -ReferenceObject $manifestSampleFrames -DifferenceObject $computedSampleFrames).Count -eq 0)
    $allPairsMatch = @($pairs | Where-Object { -not ($_.startFrameMatches -and $_.durationMatches -and $_.fillModeMatches) }).Count -eq 0
    $defaultSequenceMatches = $motionInfo.defaultSequenceId -eq $unityInfo.defaultSequenceId

    $report = [pscustomobject]@{
        motionJsonPath = Resolve-AbsolutePath $Timeline.motionJsonPath
        unityGeneratedMotionPath = Resolve-AbsolutePath $Timeline.unityGeneratedMotionPath
        defaultSequenceMatches = $defaultSequenceMatches
        sampleFramesMatch = $sampleFramesMatch
        manifestSampleFrames = $manifestSampleFrames
        computedSampleFrames = $computedSampleFrames
        itemParity = $pairs
        passed = $defaultSequenceMatches -and $sampleFramesMatch -and $allPairsMatch
    }

    $reportPath = Resolve-AbsolutePath (Join-Path $ArtifactsRoot $Timeline.metadataReport)
    $reportDirectory = Split-Path -Parent $reportPath
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory))
    {
        New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    }

    $report | ConvertTo-Json -Depth 10 | Set-Content -Path $reportPath

    if (-not $report.passed)
    {
        throw "CharPortrait motion metadata parity failed. See '$reportPath'."
    }
}

function Invoke-ScoreImagePair(
    [string]$ReferencePath,
    [string]$CandidatePath,
    [double]$Threshold,
    [int]$Tolerance,
    [string]$ReportPath,
    [string]$DiffPath,
    [string]$VisualIrPath = "",
    [string]$VisualRefinementPath = "",
    [string]$ActualLayoutPath = "",
    [string]$MeasuredLayoutPath = "")
{
    if (-not (Test-Path $ReferencePath) -or -not (Test-Path $CandidatePath))
    {
        return $null
    }

    $reportDirectory = Split-Path -Parent $ReportPath
    $diffDirectory = Split-Path -Parent $DiffPath
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory))
    {
        New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($diffDirectory))
    {
        New-Item -ItemType Directory -Force -Path $diffDirectory | Out-Null
    }

    $arguments = @(
        "run",
        "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
        "--",
        "baseline", "score",
        "--reference", $ReferencePath,
        "--candidate", $CandidatePath,
        "--normalize", "cover",
        "--tolerance", $Tolerance,
        "--out", $ReportPath,
        "--diff", $DiffPath
    )
    if (-not [string]::IsNullOrWhiteSpace($VisualIrPath) -and (Test-Path $VisualIrPath))
    {
        $arguments += @("--visual-ir", $VisualIrPath)
        if (-not [string]::IsNullOrWhiteSpace($VisualRefinementPath))
        {
            $arguments += @("--visual-refinement-out", $VisualRefinementPath)
        }
        if (-not [string]::IsNullOrWhiteSpace($ActualLayoutPath) -and (Test-Path $ActualLayoutPath))
        {
            $arguments += @("--actual-layout", $ActualLayoutPath)
            if (-not [string]::IsNullOrWhiteSpace($MeasuredLayoutPath))
            {
                $arguments += @("--measured-layout-out", $MeasuredLayoutPath)
            }
        }
    }

    Invoke-DotNetCli $arguments | Out-Host

    return Get-Content $ReportPath -Raw | ConvertFrom-Json -Depth 10
}

function Get-ReportNumber([object]$Report, [string]$PropertyName)
{
    $property = $Report.PSObject.Properties.Match($PropertyName) | Select-Object -First 1
    if ($null -eq $property)
    {
        throw "Scoring report is missing numeric property '$PropertyName'."
    }

    return [double]$property.Value
}

function Get-ReportNullableBoolean([object]$Report, [string]$PropertyName)
{
    $property = $Report.PSObject.Properties.Match($PropertyName) | Select-Object -First 1
    if ($null -eq $property -or $null -eq $property.Value)
    {
        return $null
    }

    return [bool]$property.Value
}

if ([string]::IsNullOrWhiteSpace($RepoRoot))
{
    $scriptRoot = Split-Path -Parent $PSCommandPath
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
}

if ([string]::IsNullOrWhiteSpace($UnityProjectPath))
{
    $UnityProjectPath = Join-Path $RepoRoot "samples\UnityFullPenCompare"
}

$UnityProjectPath = Resolve-AbsolutePath $UnityProjectPath

$manifestAbsolutePath = Resolve-AbsolutePath $ManifestPath
$manifest = Get-Content $manifestAbsolutePath -Raw | ConvertFrom-Json -Depth 20
$artifactsRoot = Resolve-AbsolutePath $manifest.artifactsRoot
$sourcePen = if ($manifest.PSObject.Properties["sourcePen"] -and -not [string]::IsNullOrWhiteSpace([string]$manifest.sourcePen))
{
    [string]$manifest.sourcePen
}
else
{
    "samples/pencil/full.pen"
}
$unityOutputPath = Join-Path $UnityProjectPath "Assets/Resources/BoomHudGenerated"
$uguiOutputPath = Join-Path $UnityProjectPath "Assets/BoomHudGeneratedUGui"
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null

if (-not $SkipDotNet)
{
    Write-Section "Building CLI and running targeted .NET tests"
    Invoke-DotNetCli @("build", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj")
    Invoke-DotNetCli @(
        "test", "dotnet/BoomHud.sln",
        "--filter",
        "FullyQualifiedName~PenParserTests|FullyQualifiedName~UnityGeneratorTests|FullyQualifiedName~RemotionGeneratorTests|FullyQualifiedName~PencilEndToEndTests|FullyQualifiedName~UnityMotionExporterTests"
    )

    Write-Section "Regenerating Unity and React artifacts from $sourcePen"
    $unityGenerateArgs = @(
        "run",
        "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
        "--",
        "generate", $sourcePen,
        "--target", "unity",
        "--output", $unityOutputPath,
        "--namespace", "Generated.Hud",
        "--emit-visual-ir"
    )
    if (-not [string]::IsNullOrWhiteSpace($RulesPath))
    {
        $unityGenerateArgs += @("--rules", $RulesPath)
    }
    Invoke-DotNetCli $unityGenerateArgs

    Invoke-DotNetCli @(
        "run",
        "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
        "--",
        "generate", $sourcePen,
        "--target", "react",
        "--output", "remotion/src/generated",
        "--emit-visual-ir"
    )
    $uguiGenerateArgs = @(
        "run",
        "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
        "--",
        "generate", $sourcePen,
        "--target", "ugui",
        "--output", $uguiOutputPath,
        "--namespace", "Generated.Hud.UGui",
        "--emit-visual-ir"
    )
    if (-not [string]::IsNullOrWhiteSpace($RulesPath))
    {
        $uguiGenerateArgs += @("--rules", $RulesPath)
    }
    Invoke-DotNetCli $uguiGenerateArgs
}

$timelineEntries = @($manifest.timelines)
if ($timelineEntries.Count -gt 0)
{
    Write-Section "Verifying CharPortrait sequence metadata parity"
    foreach ($timeline in $timelineEntries)
    {
        Test-CharPortraitMetadataParity -Timeline $timeline -ArtifactsRoot $artifactsRoot
    }
}

if (-not $SkipRemotion)
{
    Write-Section "Running Remotion TypeScript checks"
    Invoke-RemotionCommand @("npm", "test")
    Invoke-RemotionCommand @("npx", "tsc", "--noEmit")

    Write-Section "Rendering Remotion fidelity stills"
    Invoke-RemotionCommand @(
        "node",
        "--loader", "./node_modules/ts-node/esm.mjs",
        "--experimental-specifier-resolution=node",
        "./render-fidelity.ts",
        "--manifest", $manifestAbsolutePath
    )

    foreach ($surface in @($manifest.surfaces))
    {
        $trimTransparencyProperty = $surface.remotion.PSObject.Properties["trimTransparency"]
        $shouldTrimSurface = ($null -ne $trimTransparencyProperty) -and [bool]$trimTransparencyProperty.Value
        if ($shouldTrimSurface)
        {
            Invoke-TrimTransparency -InputPath (Resolve-AbsolutePath (Join-Path $artifactsRoot $surface.remotion.output))
        }
    }

    foreach ($timeline in $timelineEntries)
    {
        $trimTimelineProperty = $timeline.remotion.PSObject.Properties["trimTransparency"]
        $shouldTrimTimeline = ($null -ne $trimTimelineProperty) -and [bool]$trimTimelineProperty.Value
        if (-not $shouldTrimTimeline)
        {
            continue
        }

        foreach ($frame in @($timeline.sampleFrames))
        {
            $framePath = Resolve-AbsolutePath (Join-Path $artifactsRoot (Join-Path $timeline.remotion.outputDir ("frame-{0}.png" -f ([int]$frame).ToString("0000"))))
            Invoke-TrimTransparency -InputPath $framePath
        }
    }
}

if (-not $SkipUnityCapture)
{
    if ([string]::IsNullOrWhiteSpace($UnityExe))
    {
        Write-Warning "Unity capture skipped because -UnityExe was not provided."
    }
    else
    {
        Write-Section "Capturing Unity fidelity artifacts"
        Wait-ForUnityProjectAvailability -ProjectPath $UnityProjectPath
        Invoke-ExternalCommand -WorkingDirectory $RepoRoot -FilePath $UnityExe -Arguments @(
            "-batchmode",
            "-quit",
            "-projectPath", $UnityProjectPath,
            "-executeMethod", "BoomHud.Compare.Editor.BoomHudFidelityCapture.CaptureFromCommandLine",
            "--manifest", $manifestAbsolutePath
        )
        Wait-ForUnityProjectAvailability -ProjectPath $UnityProjectPath
    }
}

if (-not $SkipScoring)
{
    Write-Section "Scoring fidelity artifacts"
    $unityVisualIrDirectory = $unityOutputPath
    $reactVisualIrDirectory = Join-Path $RepoRoot "remotion/src/generated"
    $scoreSummaries = @()
    foreach ($surface in @($manifest.surfaces))
    {
        $surfaceId = [string]$surface.id
        $penPath = Resolve-AbsolutePath (Join-Path $artifactsRoot $surface.pen.output)
        $remotionPath = Resolve-AbsolutePath (Join-Path $artifactsRoot $surface.remotion.output)
        $unityPath = Resolve-AbsolutePath (Join-Path $artifactsRoot $surface.unity.output)

        $pairs = @(
            @{ name = "pen-vs-remotion"; reference = $penPath; candidate = $remotionPath },
            @{ name = "pen-vs-unity"; reference = $penPath; candidate = $unityPath },
            @{ name = "remotion-vs-unity"; reference = $remotionPath; candidate = $unityPath }
        )

        foreach ($pair in $pairs)
        {
            $visualIrPath = if ($pair.candidate -eq $unityPath) {
                Get-VisualIrArtifact -ArtifactsDirectory $unityVisualIrDirectory -Hint $surfaceId
            } elseif ($pair.candidate -eq $remotionPath) {
                Get-VisualIrArtifact -ArtifactsDirectory $reactVisualIrDirectory -Hint $surfaceId
            } else {
                $null
            }
            $actualLayoutPath = Resolve-AbsolutePath (Join-Path (Split-Path -Parent $pair.candidate) (([System.IO.Path]::GetFileNameWithoutExtension($pair.candidate)) + ".layout.actual.json"))
            $report = Invoke-ScoreImagePair `
                -ReferencePath $pair.reference `
                -CandidatePath $pair.candidate `
                -Threshold ([double]$manifest.staticThreshold) `
                -Tolerance ([int]$manifest.tolerance) `
                -ReportPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("reports/{0}-{1}.json" -f $surfaceId, $pair.name))) `
                -DiffPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("diffs/{0}-{1}.png" -f $surfaceId, $pair.name))) `
                -VisualIrPath $visualIrPath `
                -VisualRefinementPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("reports/{0}-{1}.visual-refinement.json" -f $surfaceId, $pair.name))) `
                -ActualLayoutPath $actualLayoutPath `
                -MeasuredLayoutPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("reports/{0}-{1}.measured-layout.json" -f $surfaceId, $pair.name)))

            if ($null -ne $report)
            {
                $overallSimilarity = Get-ReportNumber -Report $report -PropertyName "OverallSimilarityPercent"
                $explicitPassedThreshold = Get-ReportNullableBoolean -Report $report -PropertyName "PassedThreshold"
                $passedThreshold = if ($null -ne $explicitPassedThreshold) { $explicitPassedThreshold } else { $overallSimilarity -ge [double]$manifest.staticThreshold }
                $scoreSummaries += [pscustomobject]@{
                    id = $surfaceId
                    pair = $pair.name
                    overallSimilarityPercent = $overallSimilarity
                    passedThreshold = $passedThreshold
                }
            }
        }
    }

    foreach ($timeline in $timelineEntries)
    {
        foreach ($frame in @($timeline.sampleFrames))
        {
            $frameName = "frame-{0}" -f ([int]$frame).ToString("0000")
            $remotionPath = Resolve-AbsolutePath (Join-Path $artifactsRoot (Join-Path $timeline.remotion.outputDir ($frameName + ".png")))
            $unityPath = Resolve-AbsolutePath (Join-Path $artifactsRoot (Join-Path $timeline.unity.outputDir ($frameName + ".png")))
            $actualLayoutPath = Resolve-AbsolutePath (Join-Path (Split-Path -Parent $unityPath) (([System.IO.Path]::GetFileNameWithoutExtension($unityPath)) + ".layout.actual.json"))
            $report = Invoke-ScoreImagePair `
                -ReferencePath $remotionPath `
                -CandidatePath $unityPath `
                -Threshold ([double]$manifest.timelineThreshold) `
                -Tolerance ([int]$manifest.tolerance) `
                -ReportPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("reports/{0}-{1}.json" -f $timeline.id, $frameName))) `
                -DiffPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("diffs/{0}-{1}.png" -f $timeline.id, $frameName))) `
                -VisualIrPath (Get-VisualIrArtifact -ArtifactsDirectory $unityVisualIrDirectory -Hint $timeline.id) `
                -VisualRefinementPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("reports/{0}-{1}.visual-refinement.json" -f $timeline.id, $frameName))) `
                -ActualLayoutPath $actualLayoutPath `
                -MeasuredLayoutPath (Resolve-AbsolutePath (Join-Path $artifactsRoot ("reports/{0}-{1}.measured-layout.json" -f $timeline.id, $frameName)))

            if ($null -ne $report)
            {
                $overallSimilarity = Get-ReportNumber -Report $report -PropertyName "OverallSimilarityPercent"
                $explicitPassedThreshold = Get-ReportNullableBoolean -Report $report -PropertyName "PassedThreshold"
                $passedThreshold = if ($null -ne $explicitPassedThreshold) { $explicitPassedThreshold } else { $overallSimilarity -ge [double]$manifest.timelineThreshold }
                $scoreSummaries += [pscustomobject]@{
                    id = $timeline.id
                    pair = $frameName
                    overallSimilarityPercent = $overallSimilarity
                    passedThreshold = $passedThreshold
                }
            }
        }
    }

    $summaryPath = Resolve-AbsolutePath (Join-Path $artifactsRoot "reports/summary.json")
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $summaryPath) | Out-Null
    $scoreSummaries | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath
}

Write-Section "Fidelity workflow complete"
Write-Host "Manifest: $manifestAbsolutePath"
Write-Host "Artifacts: $artifactsRoot"
