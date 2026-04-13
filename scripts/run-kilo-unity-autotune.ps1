[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$PenPath = "samples\pencil\full.pen",
    [string]$UnityProjectPath = "",
    [string]$UnityCaptureToolRoot = "C:\Users\User\project-ultima-magic\ultima-magic",
    [string]$ReferenceImage = "",
    [ValidateSet("full", "sidebar", "charportrait")]
    [string]$RegionPreset = "full",
    [string]$TargetFile = "",
    [string]$PromptTemplate = "",
    [string]$Model = "",
    [string]$Variant = "",
    [int]$KiloTimeoutSeconds = 600,
    [int]$UnityPid = 0,
    [ValidateSet("strict", "frontier")]
    [string]$OptimizerMode = "strict",
    [int]$BeamWidth = 5,
    [int]$SearchDepth = 3,
    [int]$ExpansionBudget = 6,
    [switch]$KeepRejectedChanges,
    [switch]$SkipKilo
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $PSCommandPath

if ([string]::IsNullOrWhiteSpace($RepoRoot))
{
    $RepoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
}

if ([string]::IsNullOrWhiteSpace($UnityProjectPath))
{
    $UnityProjectPath = Join-Path $RepoRoot "samples\UnityFullPenCompare"
}

if ([string]::IsNullOrWhiteSpace($ReferenceImage))
{
    if ($RegionPreset -eq "charportrait")
    {
        $ReferenceImage = Join-Path $RepoRoot "build\_artifacts\latest\screenshots\j8BT0.png"
    }
    else
    {
        $ReferenceImage = Join-Path $RepoRoot "build\_artifacts\latest\screenshots\pencil-fullpen-window-crop2.png"
    }
}

if ([string]::IsNullOrWhiteSpace($TargetFile))
{
    $TargetFile = Join-Path $RepoRoot "dotnet\src\BoomHud.Gen.Unity\UnityGenerator.cs"
}

if ([string]::IsNullOrWhiteSpace($PromptTemplate))
{
    $PromptTemplate = Join-Path $RepoRoot ".kilocode\prompts\unity-generator-autotune.md"
}

$cliProject = Join-Path $RepoRoot "dotnet\src\BoomHud.Cli\BoomHud.Cli.csproj"
$unitTestProject = Join-Path $RepoRoot "dotnet\tests\BoomHud.Tests.Unit\BoomHud.Tests.Unit.csproj"
$generatedOutput = Join-Path $UnityProjectPath "Assets\Resources\BoomHudGenerated"
$captureScript = Join-Path $UnityCaptureToolRoot "tools\capture_window_region.py"
$attemptRoot = Join-Path $RepoRoot "build\_artifacts\latest\kilo-unity-autotune"
$attemptId = Get-Date -Format "yyyyMMdd-HHmmss"
$attemptDir = Join-Path $attemptRoot $attemptId
$beforeDir = Join-Path $attemptDir "before"
$afterDir = Join-Path $attemptDir "after"
$backupDir = Join-Path $attemptDir "backup"
$summaryPath = Join-Path $attemptDir "summary.json"

$unityCrop = @{
    Left = 1120
    Top = 295
    Right = 2510
    Bottom = 1315
}

$regionCropRatios = @{
    sidebar = @{
        Reference = @{
            Left = 0.762153
            Top = 0.023645
            Right = 0.976565
            Bottom = 0.976474
        }
        Candidate = @{
            Left = 0.705
            Top = 0.021859
            Right = 0.874007
            Bottom = 0.988
        }
    }
    charportrait = @{
        Reference = @{
            Left = 0.0
            Top = 0.0
            Right = 1.0
            Bottom = 1.0
        }
        Candidate = @{
            Left = 0.0381
            Top = 0.4078
            Right = 0.1374
            Bottom = 0.5725
        }
    }
}

function Write-Section([string]$Message)
{
    Write-Host ""
    Write-Host "==> $Message"
}

function Get-DescendantProcessIds([int]$RootProcessId)
{
    $all = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId
    $pending = [System.Collections.Generic.Queue[int]]::new()
    $pending.Enqueue($RootProcessId)
    $descendants = [System.Collections.Generic.List[int]]::new()

    while ($pending.Count -gt 0)
    {
        $current = $pending.Dequeue()
        $children = $all | Where-Object { $_.ParentProcessId -eq $current } | Select-Object -ExpandProperty ProcessId
        foreach ($child in $children)
        {
            $descendants.Add([int]$child)
            $pending.Enqueue([int]$child)
        }
    }

    return $descendants
}

function Stop-ProcessTree([int]$RootProcessId)
{
    $descendants = Get-DescendantProcessIds -RootProcessId $RootProcessId
    foreach ($processId in ($descendants | Sort-Object -Descending))
    {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }

    Stop-Process -Id $RootProcessId -Force -ErrorAction SilentlyContinue
}

function Get-UnityProcessId([string]$ProjectPath, [int]$PreferredPid)
{
    if ($PreferredPid -gt 0)
    {
        return $PreferredPid
    }

    $normalizedProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).ToLowerInvariant()
    $processes = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.ToLowerInvariant().Contains($normalizedProjectPath) }

    $foreground = $processes |
        Where-Object { $_.CommandLine -notmatch "-batchMode" } |
        Select-Object -First 1

    if ($foreground)
    {
        return [int]$foreground.ProcessId
    }

    throw "Could not find a foreground Unity.exe process for project '$ProjectPath'."
}

function Invoke-DotNet([string[]]$Arguments, [string]$WorkingDirectory)
{
    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Get-MainVisualIrArtifact([string]$ArtifactsDirectory)
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

    return $artifacts[0].FullName
}

function Invoke-PythonCrop([string]$InputPath, [string]$OutputPath, [hashtable]$Crop)
{
    $script = @'
import argparse
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("--input", required=True)
parser.add_argument("--output", required=True)
parser.add_argument("--left", type=int, required=True)
parser.add_argument("--top", type=int, required=True)
parser.add_argument("--right", type=int, required=True)
parser.add_argument("--bottom", type=int, required=True)
args = parser.parse_args()

with Image.open(args.input) as image:
    cropped = image.crop((args.left, args.top, args.right, args.bottom))
    cropped.save(args.output)
'@

    $script | python - `
        --input $InputPath `
        --output $OutputPath `
        --left $Crop.Left `
        --top $Crop.Top `
        --right $Crop.Right `
        --bottom $Crop.Bottom | Out-Host

    if ($LASTEXITCODE -ne 0)
    {
        throw "Image crop failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PythonRelativeCrop([string]$InputPath, [string]$OutputPath, [hashtable]$CropRatios)
{
    $script = @'
import argparse
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("--input", required=True)
parser.add_argument("--output", required=True)
parser.add_argument("--left", type=float, required=True)
parser.add_argument("--top", type=float, required=True)
parser.add_argument("--right", type=float, required=True)
parser.add_argument("--bottom", type=float, required=True)
args = parser.parse_args()

with Image.open(args.input) as image:
    width, height = image.size
    left = int(round(width * args.left))
    top = int(round(height * args.top))
    right = int(round(width * args.right))
    bottom = int(round(height * args.bottom))
    cropped = image.crop((left, top, right, bottom))
    cropped.save(args.output)
'@

    $script | python - `
        --input $InputPath `
        --output $OutputPath `
        --left $CropRatios.Left `
        --top $CropRatios.Top `
        --right $CropRatios.Right `
        --bottom $CropRatios.Bottom | Out-Host

    if ($LASTEXITCODE -ne 0)
    {
        throw "Relative image crop failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Generate()
{
    Write-Section "Regenerating Unity artifacts"
    Invoke-DotNet -Arguments @(
        "run",
        "--project", $cliProject,
        "--",
        "generate", $PenPath,
        "--target", "unity",
        "--output", $generatedOutput,
        "--namespace", "Generated.Hud",
        "--emit-visual-ir"
    ) -WorkingDirectory $RepoRoot
}

function Invoke-UnityGeneratorTests()
{
    Write-Section "Running Unity generator tests"
    Invoke-DotNet -Arguments @("test", $unitTestProject) -WorkingDirectory $RepoRoot
}

function Invoke-ScoreStage([string]$StageDir, [int]$ProcessId)
{
    New-Item -ItemType Directory -Force -Path $StageDir | Out-Null

    Invoke-Generate
    Start-Sleep -Seconds 8

    $windowCapture = Join-Path $StageDir "unity-window.png"
    $cropCapture = Join-Path $StageDir "unity-window-crop.png"
    $referenceScoreImage = $ReferenceImage
    $candidateScoreImage = $cropCapture
    $reportPath = Join-Path $StageDir "score-report.json"
    $diffPath = Join-Path $StageDir "score-diff.png"

    Write-Section "Capturing Unity window"
    $env:PYTHONIOENCODING = "utf-8"
    & python $captureScript --pid $ProcessId --output $windowCapture | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "Unity window capture failed with exit code $LASTEXITCODE."
    }

    Write-Section "Cropping Unity HUD region"
    Invoke-PythonCrop -InputPath $windowCapture -OutputPath $cropCapture -Crop $unityCrop

    if ($RegionPreset -ne "full")
    {
        $preset = $regionCropRatios[$RegionPreset]
        if ($null -eq $preset)
        {
            throw "Unsupported region preset '$RegionPreset'."
        }

        $referenceRegion = Join-Path $StageDir "reference-$RegionPreset.png"
        $candidateRegion = Join-Path $StageDir "candidate-$RegionPreset.png"

        Write-Section "Cropping region preset '$RegionPreset'"
        Invoke-PythonRelativeCrop -InputPath $ReferenceImage -OutputPath $referenceRegion -CropRatios $preset.Reference
        Invoke-PythonRelativeCrop -InputPath $cropCapture -OutputPath $candidateRegion -CropRatios $preset.Candidate

        $referenceScoreImage = $referenceRegion
        $candidateScoreImage = $candidateRegion
    }

    Write-Section "Scoring cropped pair"
    $scoreArgs = @(
        "run",
        "--project", $cliProject,
        "--",
        "baseline", "score",
        "--reference", $referenceScoreImage,
        "--candidate", $candidateScoreImage,
        "--normalize", "cover",
        "--diff", $diffPath,
        "--out", $reportPath,
        "--tolerance", "8"
    )
    $visualIrPath = Get-MainVisualIrArtifact -ArtifactsDirectory $generatedOutput
    if (-not [string]::IsNullOrWhiteSpace($visualIrPath))
    {
        $scoreArgs += @(
            "--visual-ir", $visualIrPath,
            "--visual-refinement-out", (Join-Path $StageDir "score-refinement.json")
        )
    }

    Invoke-DotNet -Arguments $scoreArgs -WorkingDirectory $RepoRoot

    $report = Get-Content $reportPath -Raw | ConvertFrom-Json
    return [pscustomobject]@{
        WindowCapture = $windowCapture
        CropCapture = $cropCapture
        ReferenceScoreImage = $referenceScoreImage
        CandidateScoreImage = $candidateScoreImage
        ReportPath = $reportPath
        DiffPath = $diffPath
        OverallSimilarityPercent = [double]$report.OverallSimilarityPercent
        PixelIdentityPercent = [double]$report.PixelIdentityPercent
        ChangedPercent = [double]$report.Metrics.changedPercent
    }
}

function Write-AutotuneOptimizerSummary(
    [string]$Path,
    [string]$Label,
    [string]$SurfaceId,
    [object]$Score)
{
    $summary = [ordered]@{
        label = $Label
        averageOverallSimilarityPercent = [double]$Score.OverallSimilarityPercent
        surfaces = @(
            [ordered]@{
                id = $SurfaceId
                reportPath = $Score.ReportPath
                measuredLayoutPath = $null
            }
        )
    }

    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path
}

function Invoke-FrontierAcceptance(
    [string]$AttemptDirectory,
    [object]$BaselineScore,
    [object]$CandidateScore)
{
    $surfaceId = "autotune-$RegionPreset-ugui"
    $baselineSummaryPath = Join-Path $AttemptDirectory "optimizer-baseline.summary.json"
    $candidateSummaryPath = Join-Path $AttemptDirectory "optimizer-candidate.summary.json"
    $statePath = Join-Path $AttemptDirectory "optimizer.state.json"
    $summaryPath = Join-Path $AttemptDirectory "optimizer-summary.json"

    Write-AutotuneOptimizerSummary -Path $baselineSummaryPath -Label "baseline" -SurfaceId $surfaceId -Score $BaselineScore
    Write-AutotuneOptimizerSummary -Path $candidateSummaryPath -Label "candidate" -SurfaceId $surfaceId -Score $CandidateScore

    $state = [ordered]@{
        optimizerMode = "frontier"
        beamWidth = $BeamWidth
        searchDepth = $SearchDepth
        expansionBudget = $ExpansionBudget
        primarySurfaceId = $surfaceId
        baselineCandidateId = "baseline"
        candidates = @(
            [ordered]@{
                candidateId = "baseline"
                label = "baseline"
                summaryPath = $baselineSummaryPath
                parentCandidateId = $null
                depth = 0
                appliedActions = @()
            },
            [ordered]@{
                candidateId = "candidate"
                label = "candidate"
                summaryPath = $candidateSummaryPath
                parentCandidateId = "baseline"
                depth = 1
                appliedActions = @("kilo-patch")
            }
        )
    }
    $state | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $statePath

    Invoke-DotNet -Arguments @(
        "run",
        "--project", $cliProject,
        "--",
        "rules", "frontier-optimize",
        "--input", $statePath,
        "--out", $summaryPath,
        "--summary", "false"
    ) -WorkingDirectory $RepoRoot

    $optimizerSummary = Get-Content $summaryPath -Raw | ConvertFrom-Json
    return [pscustomobject]@{
        Accepted = ([string]$optimizerSummary.selectedCandidateId) -eq "candidate"
        SummaryPath = $summaryPath
        SelectedCandidateId = [string]$optimizerSummary.selectedCandidateId
    }
}

function Invoke-KiloAttempt([string]$PromptPath, [string]$LogPath)
{
    $kiloCommand = Get-Command kilo -ErrorAction Stop
    $powershellExe = (Get-Command powershell -ErrorAction Stop).Source
    $commandText = "& '$($kiloCommand.Source)' run --dir '$RepoRoot' --auto --format json"

    if (-not [string]::IsNullOrWhiteSpace($Model))
    {
        $commandText += " --model '$Model'"
    }

    if (-not [string]::IsNullOrWhiteSpace($Variant))
    {
        $commandText += " --variant '$Variant'"
    }

    $commandText += " (Get-Content -Raw '$PromptPath')"

    $stderrPath = [System.IO.Path]::ChangeExtension($LogPath, ".stderr.txt")
    $process = Start-Process -FilePath $powershellExe `
        -ArgumentList @("-ExecutionPolicy", "Bypass", "-Command", $commandText) `
        -WorkingDirectory $RepoRoot `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput $LogPath `
        -RedirectStandardError $stderrPath

    try
    {
        if (-not $process.WaitForExit($KiloTimeoutSeconds * 1000))
        {
            throw "kilo run exceeded timeout of $KiloTimeoutSeconds seconds."
        }

        $process.WaitForExit()
        $process.Refresh()
        $exitCode = $process.ExitCode
        if ($null -ne $exitCode -and $exitCode -ne 0)
        {
            throw "kilo run failed with exit code $exitCode."
        }
    }
    finally
    {
        if (-not $process.HasExited)
        {
            Stop-ProcessTree -RootProcessId $process.Id
            $null = $process.WaitForExit(5000)
        }
    }
}

function Restore-TargetFile([string]$BackupPath)
{
    Copy-Item -LiteralPath $BackupPath -Destination $TargetFile -Force
    Invoke-Generate
}

function Test-FileChanged([string]$OriginalPath, [string]$CandidatePath)
{
    if (-not (Test-Path $OriginalPath) -or -not (Test-Path $CandidatePath))
    {
        return $false
    }

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $OriginalPath).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $CandidatePath).Hash
}

New-Item -ItemType Directory -Force -Path $attemptDir, $beforeDir, $afterDir, $backupDir | Out-Null

Write-Section "Resolving Unity process"
$resolvedUnityPid = Get-UnityProcessId -ProjectPath $UnityProjectPath -PreferredPid $UnityPid

Write-Section "Capturing baseline score"
$beforeScore = Invoke-ScoreStage -StageDir $beforeDir -ProcessId $resolvedUnityPid

$backupTarget = Join-Path $backupDir ([System.IO.Path]::GetFileName($TargetFile))
Copy-Item -LiteralPath $TargetFile -Destination $backupTarget -Force

$promptText = (Get-Content $PromptTemplate -Raw).Replace("{{BASELINE_SCORE}}", $beforeScore.OverallSimilarityPercent.ToString("F2", [System.Globalization.CultureInfo]::InvariantCulture))
$promptPath = Join-Path $attemptDir "kilo-prompt.md"
Set-Content -LiteralPath $promptPath -Value $promptText -NoNewline

$kiloLogPath = Join-Path $attemptDir "kilo-output.txt"
$kiloRan = $false
$testPassed = $false
$restored = $false
$accepted = $false
$violation = $null
$kiloWarning = $null
$recoveredTimedOutPatch = $false
$optimizerResult = $null

try
{
    if (-not $SkipKilo)
    {
        Write-Section "Running Kilo attempt"
        try
        {
            Invoke-KiloAttempt -PromptPath $promptPath -LogPath $kiloLogPath
            $kiloRan = $true
        }
        catch
        {
            $kiloWarning = $_.Exception.Message
            if (
                $kiloWarning -like "kilo run exceeded timeout*" -and
                (Test-FileChanged -OriginalPath $backupTarget -CandidatePath $TargetFile)
            )
            {
                Write-Section "Kilo timed out after modifying target file; continuing with validation"
                $kiloRan = $true
                $recoveredTimedOutPatch = $true
            }
            else
            {
                throw
            }
        }
    }

    Invoke-UnityGeneratorTests
    $testPassed = $true

    Write-Section "Capturing candidate score"
    $afterScore = Invoke-ScoreStage -StageDir $afterDir -ProcessId $resolvedUnityPid

    if ($OptimizerMode -eq "frontier")
    {
        $optimizerResult = Invoke-FrontierAcceptance -AttemptDirectory $attemptDir -BaselineScore $beforeScore -CandidateScore $afterScore
        $accepted = [bool]$optimizerResult.Accepted
    }
    else
    {
        $accepted = $afterScore.OverallSimilarityPercent -gt $beforeScore.OverallSimilarityPercent
    }
    if (-not $accepted -and -not $KeepRejectedChanges)
    {
        Write-Section "Restoring rejected attempt"
        Restore-TargetFile -BackupPath $backupTarget
        $restored = $true
    }
}
catch
{
    $violation = $_.Exception.Message
    if (-not $KeepRejectedChanges)
    {
        Write-Section "Restoring failed attempt"
        Restore-TargetFile -BackupPath $backupTarget
        $restored = $true
    }

    $afterScore = $null
}

$sessionId = $null
$finalText = $null
if (Test-Path $kiloLogPath)
{
    $finalText = Get-Content $kiloLogPath -Raw
    if (-not [string]::IsNullOrWhiteSpace($finalText))
    {
        Set-Content -LiteralPath (Join-Path $attemptDir "kilo-final.txt") -Value $finalText -NoNewline
    }
}

$summary = [pscustomobject]@{
    AttemptId = $attemptId
    AttemptDir = $attemptDir
    PromptPath = $promptPath
    KiloLogPath = if (Test-Path $kiloLogPath) { $kiloLogPath } else { $null }
    KiloSessionId = $sessionId
    KiloRan = $kiloRan
    KiloWarning = $kiloWarning
    RecoveredTimedOutPatch = $recoveredTimedOutPatch
    OptimizerMode = $OptimizerMode
    OptimizerResult = $optimizerResult
    TestPassed = $testPassed
    Accepted = $accepted
    Restored = $restored
    Failure = $violation
    TargetFile = $TargetFile
    UnityPid = $resolvedUnityPid
    Baseline = $beforeScore
    Candidate = $afterScore
}

$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -NoNewline

Write-Host ""
Write-Host "Attempt directory: $attemptDir"
Write-Host "Baseline overall: $($beforeScore.OverallSimilarityPercent.ToString("F2"))%"
if ($afterScore)
{
    Write-Host "Candidate overall: $($afterScore.OverallSimilarityPercent.ToString("F2"))%"
}
if ($kiloWarning)
{
    Write-Host "Kilo warning: $kiloWarning"
}
if ($optimizerResult)
{
    Write-Host "Optimizer summary: $($optimizerResult.SummaryPath)"
    Write-Host "Optimizer selected: $($optimizerResult.SelectedCandidateId)"
}
if ($accepted)
{
    Write-Host "Result: accepted"
}
elseif ($violation)
{
    Write-Host "Result: failed"
    Write-Host "Failure: $violation"
}
else
{
    Write-Host "Result: rejected"
}
if ($restored)
{
    Write-Host "Workspace: target file restored to pre-attempt state"
}
