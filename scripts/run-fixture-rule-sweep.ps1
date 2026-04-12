[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$RuleManifestGlob = "build/fixture-manifests/rules/*.json",
    [string]$CompareManifestPath = "build/fixture-manifests/fixture-compare.json",
    [string]$MotionManifestPath = "fidelity/pen-remotion-unity.fullpen.json",
    [string]$UnityProjectPath = "",
    [string]$UnityExe = "",
    [string]$OutputRoot = "build/fixture-rule-sweeps/latest",
    [int]$Tolerance = 8,
    [string]$Normalization = "stretch",
    [string[]]$PlanningFacts = @(
        "finding.text-or-icon-metrics-mismatch=present",
        "finding.edge-alignment-mismatch=present",
        "motion.enabled=true"
    ),
    [switch]$SkipBaseline,
    [switch]$NoRestoreDefault
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($PlanningFacts.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace($PlanningFacts[0]) -and $PlanningFacts[0].Contains(";"))
{
    $PlanningFacts = @(
        $PlanningFacts[0].Split(';', [System.StringSplitOptions]::RemoveEmptyEntries) |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

function Write-Section([string]$Message)
{
    Write-Host ""
    Write-Host "==> $Message"
}

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

function Resolve-PowerShellExecutable()
{
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -ne $pwsh)
    {
        return $pwsh.Source
    }

    if ($IsWindows)
    {
        return "powershell"
    }

    return "pwsh"
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

function Wait-ForFileReady(
    [string]$Path,
    [int]$TimeoutSeconds = 30)
{
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline)
    {
        if ((Test-Path $Path) -and ((Get-Item $Path).Length -gt 0))
        {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for file '$Path'."
}

function Get-OptionalPropertyValue(
    [object]$Source,
    [string]$PropertyName)
{
    if ($null -eq $Source)
    {
        return $null
    }

    $property = $Source.PSObject.Properties[$PropertyName]
    if ($null -eq $property)
    {
        return $null
    }

    return $property.Value
}

function Get-ReferencePathForSurface([string]$SurfaceId)
{
    switch ($SurfaceId)
    {
        "party-status-strip-uitk" { return Resolve-AbsolutePath "build/fixture-refs/party-status-strip/PSS01.png" }
        "party-status-strip-ugui" { return Resolve-AbsolutePath "build/fixture-refs/party-status-strip/PSS01.png" }
        "quest-sidebar-uitk" { return Resolve-AbsolutePath "build/fixture-refs/quest-sidebar/QSB01.png" }
        "quest-sidebar-ugui" { return Resolve-AbsolutePath "build/fixture-refs/quest-sidebar/QSB01.png" }
        "combat-toast-stack-uitk" { return Resolve-AbsolutePath "build/fixture-refs/combat-toast-stack/CTS01.png" }
        "combat-toast-stack-ugui" { return Resolve-AbsolutePath "build/fixture-refs/combat-toast-stack/CTS01.png" }
        default { throw "No reference image is configured for surface '$SurfaceId'." }
    }
}

function Get-ImageDimensions([string]$ImagePath)
{
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($ImagePath)
    try
    {
        return [pscustomobject]@{
            Width = [int]$image.Width
            Height = [int]$image.Height
        }
    }
    finally
    {
        $image.Dispose()
    }
}

function New-RunCompareManifest(
    [string]$BaseManifestPath,
    [string]$RunRoot,
    [string]$Label)
{
    $manifest = Get-Content $BaseManifestPath -Raw | ConvertFrom-Json -Depth 20
    $manifest.artifactsRoot = $RunRoot

    foreach ($surface in @($manifest.surfaces))
    {
        $captureFileName = "$($surface.id).png"
        $surface.unity.output = (Join-Path $RunRoot (Join-Path "captures" $captureFileName))
        if (-not [string]::IsNullOrWhiteSpace([string](Get-OptionalPropertyValue -Source $surface.unity -PropertyName "targetObjectName")))
        {
            $referencePath = Get-ReferencePathForSurface -SurfaceId $surface.id
            $dimensions = Get-ImageDimensions -ImagePath $referencePath

            if ($null -eq $surface.unity.PSObject.Properties["captureWidth"])
            {
                $surface.unity | Add-Member -NotePropertyName "captureWidth" -NotePropertyValue $dimensions.Width
            }
            else
            {
                $surface.unity.captureWidth = $dimensions.Width
            }

            if ($null -eq $surface.unity.PSObject.Properties["captureHeight"])
            {
                $surface.unity | Add-Member -NotePropertyName "captureHeight" -NotePropertyValue $dimensions.Height
            }
            else
            {
                $surface.unity.captureHeight = $dimensions.Height
            }
        }
    }

    $manifestsRoot = Join-Path $RunRoot "manifests"
    New-Item -ItemType Directory -Force -Path $manifestsRoot | Out-Null
    $runManifestPath = Join-Path $manifestsRoot "$Label.compare.json"
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path $runManifestPath
    return $runManifestPath
}

function New-RunMotionManifest(
    [string]$BaseManifestPath,
    [string]$RunRoot,
    [string]$Label)
{
    $manifest = Get-Content $BaseManifestPath -Raw | ConvertFrom-Json -Depth 20
    $manifest.artifactsRoot = (Join-Path $RunRoot "motion")

    $manifestsRoot = Join-Path $RunRoot "manifests"
    New-Item -ItemType Directory -Force -Path $manifestsRoot | Out-Null
    $runManifestPath = Join-Path $manifestsRoot "$Label.motion.json"
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path $runManifestPath
    return $runManifestPath
}

function New-PlannedRuleArtifact(
    [string]$RulePath,
    [string]$RunRoot,
    [string]$Label)
{
    if ([string]::IsNullOrWhiteSpace($RulePath))
    {
        return [pscustomobject]@{
            executableRulesPath = ""
            planSummaryPath = ""
            selectedActions = @()
        }
    }

    $plansRoot = Join-Path $RunRoot "plans"
    New-Item -ItemType Directory -Force -Path $plansRoot | Out-Null

    $planPath = Join-Path $plansRoot "$Label.plan.json"
    $plannedRulesPath = Join-Path $plansRoot "$Label.rules.json"
    $planArgs = @(
        "run",
        "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
        "--",
        "rules", "plan",
        "--rules", $RulePath,
        "--out", $planPath,
        "--emit-rules", $plannedRulesPath
    )

    foreach ($fact in @($PlanningFacts))
    {
        if (-not [string]::IsNullOrWhiteSpace($fact))
        {
            $planArgs += @("--fact", $fact)
        }
    }

    Invoke-DotNetCli -Arguments $planArgs | Out-Host

    $plan = Get-Content $planPath -Raw | ConvertFrom-Json -Depth 20
    $selectedActions = @($plan.appliedRules | ForEach-Object {
        if (-not [string]::IsNullOrWhiteSpace([string]$_.name))
        {
            [string]$_.name
        }
    })

    return [pscustomobject]@{
        executableRulesPath = $plannedRulesPath
        planSummaryPath = $planPath
        selectedActions = $selectedActions
    }
}

function Invoke-GenerateFixtureSet([string]$RulesPath)
{
    $unityOutputPath = Join-Path $UnityProjectPath "Assets/Resources/BoomHudGenerated"
    $uguiOutputPath = Join-Path $UnityProjectPath "Assets/BoomHudGeneratedUGui"
    $fixtures = @(
        [pscustomobject]@{
            Source = Resolve-AbsolutePath "samples/pencil/party-status-strip.pen"
            Root = "PartyStatusStrip"
        },
        [pscustomobject]@{
            Source = Resolve-AbsolutePath "samples/pencil/quest-sidebar.pen"
            Root = "QuestSidebar"
        },
        [pscustomobject]@{
            Source = Resolve-AbsolutePath "samples/pencil/combat-toast-stack.pen"
            Root = "CombatToastStack"
        }
    )

    foreach ($fixture in $fixtures)
    {
        $unityArgs = @(
            "run",
            "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
            "--",
            "generate", $fixture.Source,
            "--target", "unity",
            "--output", $unityOutputPath,
            "--namespace", "Generated.Hud",
            "--emit-visual-ir"
        )

        $uguiArgs = @(
            "run",
            "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
            "--",
            "generate", $fixture.Source,
            "--target", "ugui",
            "--output", $uguiOutputPath,
            "--namespace", "Generated.Hud.UGui",
            "--emit-visual-ir"
        )

        if (-not [string]::IsNullOrWhiteSpace($RulesPath))
        {
            $unityArgs += @("--rules", $RulesPath)
            $uguiArgs += @("--rules", $RulesPath)
        }

        Invoke-DotNetCli -Arguments $unityArgs
        Invoke-DotNetCli -Arguments $uguiArgs
    }
}

function Invoke-UnityCapture([string]$ManifestPath)
{
    if ([string]::IsNullOrWhiteSpace($UnityExe))
    {
        throw "Unity capture requires -UnityExe."
    }

    Wait-ForUnityProjectAvailability -ProjectPath $UnityProjectPath
    Invoke-ExternalCommand -WorkingDirectory $RepoRoot -FilePath $UnityExe -Arguments @(
        "-batchmode",
        "-quit",
        "-projectPath", $UnityProjectPath,
        "-executeMethod", "BoomHud.Compare.Editor.BoomHudFidelityCapture.CaptureFromCommandLine",
        "--manifest", $ManifestPath
    )
    Wait-ForUnityProjectAvailability -ProjectPath $UnityProjectPath
}

function Invoke-ScoreRun(
    [string]$ManifestPath,
    [string]$RunRoot,
    [string]$Label,
    [string]$RulePath,
    [string]$PlannedRulePath,
    [string]$PlanSummaryPath,
    [object[]]$SelectedActions)
{
    $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json -Depth 20
    $scoresRoot = Join-Path $RunRoot "scores"
    New-Item -ItemType Directory -Force -Path $scoresRoot | Out-Null
    $unityVisualIrDirectory = Join-Path $UnityProjectPath "Assets/Resources/BoomHudGenerated"
    $uguiVisualIrDirectory = Join-Path $UnityProjectPath "Assets/BoomHudGeneratedUGui"

    $surfaceResults = @()
    foreach ($surface in @($manifest.surfaces))
    {
        $surfaceId = [string]$surface.id
        $referencePath = Get-ReferencePathForSurface -SurfaceId $surfaceId
        $candidatePath = Resolve-AbsolutePath ([string]$surface.unity.output)
        Wait-ForFileReady -Path $candidatePath
        $reportPath = Join-Path $scoresRoot "$surfaceId.json"
        $diffPath = Join-Path $scoresRoot "$surfaceId.diff.png"
        $actualLayoutPath = Join-Path (Split-Path -Parent $candidatePath) (([System.IO.Path]::GetFileNameWithoutExtension($candidatePath)) + ".layout.actual.json")

        $scoreArgs = @(
            "run",
            "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
            "--",
            "baseline", "score",
            "--reference", $referencePath,
            "--candidate", $candidatePath,
            "--normalize", $Normalization,
            "--tolerance", $Tolerance,
            "--out", $reportPath,
            "--diff", $diffPath,
            "--summary", "true"
        )
        $visualIrDirectory = if ($surfaceId -like "*-ugui") { $uguiVisualIrDirectory } else { $unityVisualIrDirectory }
        $visualIrPath = Get-VisualIrArtifact -ArtifactsDirectory $visualIrDirectory -Hint $surfaceId

        if (-not [string]::IsNullOrWhiteSpace($visualIrPath))
        {
            $scoreArgs += @(
                "--visual-ir", $visualIrPath,
                "--visual-refinement-out", (Join-Path $scoresRoot "$surfaceId.visual-refinement.json")
            )
            if (Test-Path $actualLayoutPath)
            {
                $scoreArgs += @(
                    "--actual-layout", $actualLayoutPath,
                    "--measured-layout-out", (Join-Path $scoresRoot "$surfaceId.measured-layout.json")
                )
            }
        }

        Invoke-DotNetCli -Arguments $scoreArgs | Out-Host

        $report = Get-Content $reportPath -Raw | ConvertFrom-Json -Depth 20
        $surfaceResults += [pscustomobject]@{
            id = $surfaceId
            referencePath = $referencePath
            candidatePath = $candidatePath
            reportPath = $reportPath
            diffPath = $diffPath
            overallSimilarityPercent = [double]$report.OverallSimilarityPercent
            pixelIdentityPercent = [double]$report.PixelIdentityPercent
            deltaSimilarityPercent = [double]$report.DeltaSimilarityPercent
            probableFixArea = [string](Get-OptionalPropertyValue -Source $report -PropertyName "ProbableFixArea")
            suggestedAction = [string](Get-OptionalPropertyValue -Source $report -PropertyName "SuggestedAction")
            findings = @($report.Findings)
        }

        if ([string]::IsNullOrWhiteSpace($surfaceResults[-1].probableFixArea) -and $surfaceResults[-1].findings.Count -gt 0)
        {
            $surfaceResults[-1].probableFixArea = [string](Get-OptionalPropertyValue -Source $surfaceResults[-1].findings[0] -PropertyName "ProbableFixArea")
        }

        if ([string]::IsNullOrWhiteSpace($surfaceResults[-1].suggestedAction) -and $surfaceResults[-1].findings.Count -gt 0)
        {
            $surfaceResults[-1].suggestedAction = [string](Get-OptionalPropertyValue -Source $surfaceResults[-1].findings[0] -PropertyName "SuggestedAction")
        }
    }

    $average = if ($surfaceResults.Count -gt 0)
    {
        [Math]::Round((($surfaceResults | Measure-Object -Property overallSimilarityPercent -Average).Average), 4)
    }
    else
    {
        0d
    }

    $summary = [pscustomobject]@{
        label = $Label
        rulePath = if ([string]::IsNullOrWhiteSpace($RulePath)) { $null } else { $RulePath }
        plannedRulePath = if ([string]::IsNullOrWhiteSpace($PlannedRulePath)) { $null } else { $PlannedRulePath }
        planSummaryPath = if ([string]::IsNullOrWhiteSpace($PlanSummaryPath)) { $null } else { $PlanSummaryPath }
        selectedActions = @($SelectedActions)
        averageOverallSimilarityPercent = $average
        surfaces = $surfaceResults
    }

    $summaryPath = Join-Path $RunRoot "summary.json"
    $summary | ConvertTo-Json -Depth 20 | Set-Content -Path $summaryPath
    return $summary
}

function Invoke-MotionSweep(
    [string]$RunRoot,
    [string]$Label,
    [string]$PlannedRulePath)
{
    if (-not (Test-Path $MotionManifestPath))
    {
        return $null
    }

    $motionManifest = New-RunMotionManifest -BaseManifestPath $MotionManifestPath -RunRoot $RunRoot -Label $Label
    $scriptPath = Join-Path $RepoRoot "scripts/run-pen-remotion-unity-fidelity.ps1"
    $args = @(
        "-RepoRoot", $RepoRoot,
        "-ManifestPath", $motionManifest,
        "-SkipDotNet"
    )

    if (-not [string]::IsNullOrWhiteSpace($UnityExe))
    {
        $args += @("-UnityExe", $UnityExe)
    }

    if (-not [string]::IsNullOrWhiteSpace($UnityProjectPath))
    {
        $args += @("-UnityProjectPath", $UnityProjectPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($PlannedRulePath))
    {
        $args += @("-RulesPath", $PlannedRulePath)
    }

    $shell = Resolve-PowerShellExecutable
    Invoke-ExternalCommand -WorkingDirectory $RepoRoot -FilePath $shell -Arguments (@("-NoProfile", "-File", $scriptPath) + $args) | Out-Host

    $motionSummaryPath = Join-Path $RunRoot "motion/reports/summary.json"
    if (-not (Test-Path $motionSummaryPath))
    {
        return $null
    }

    $motionEntries = @(Get-Content $motionSummaryPath -Raw | ConvertFrom-Json -Depth 20)
    $average = if ($motionEntries.Count -gt 0)
    {
        [Math]::Round((($motionEntries | Measure-Object -Property overallSimilarityPercent -Average).Average), 4)
    }
    else
    {
        0d
    }

    return [pscustomobject]@{
        summaryPath = $motionSummaryPath
        averageOverallSimilarityPercent = $average
        entries = $motionEntries
    }
}

function New-SweepCandidate([string]$Label, [string]$RulePath)
{
    return [pscustomobject]@{
        Label = $Label
        RulePath = $RulePath
    }
}

if ([string]::IsNullOrWhiteSpace($RepoRoot))
{
    $scriptRoot = Split-Path -Parent $PSCommandPath
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
}

if ([string]::IsNullOrWhiteSpace($UnityProjectPath))
{
    $UnityProjectPath = Join-Path $RepoRoot "samples/UnityFullPenCompare"
}

$UnityProjectPath = Resolve-AbsolutePath $UnityProjectPath

$CompareManifestPath = Resolve-AbsolutePath $CompareManifestPath
$MotionManifestPath = Resolve-AbsolutePath $MotionManifestPath
$OutputRoot = Resolve-AbsolutePath $OutputRoot
$ruleSearchPath = if ([System.IO.Path]::IsPathRooted($RuleManifestGlob))
{
    $RuleManifestGlob
}
else
{
    Join-Path $RepoRoot $RuleManifestGlob
}

$ruleFiles = @(Get-ChildItem -Path $ruleSearchPath -File | Sort-Object FullName)
if ($ruleFiles.Count -eq 0)
{
    throw "No rule manifests matched '$ruleSearchPath'."
}

if (-not (Test-Path $CompareManifestPath))
{
    throw "Compare manifest not found: $CompareManifestPath"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$candidates = @()
if (-not $SkipBaseline)
{
    $candidates += New-SweepCandidate -Label "baseline-no-rules" -RulePath ""
}

foreach ($ruleFile in $ruleFiles)
{
    $candidates += New-SweepCandidate -Label $ruleFile.BaseName -RulePath $ruleFile.FullName
}

$runSummaries = @()
try
{
    foreach ($candidate in $candidates)
    {
        $runRoot = Join-Path $OutputRoot $candidate.Label
        New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
        $plannedRuleArtifact = New-PlannedRuleArtifact -RulePath $candidate.RulePath -RunRoot $runRoot -Label $candidate.Label

        Write-Section "Generating fixtures for $($candidate.Label)"
        Invoke-GenerateFixtureSet -RulesPath $plannedRuleArtifact.executableRulesPath

        Write-Section "Capturing fixtures for $($candidate.Label)"
        $runManifestPath = New-RunCompareManifest -BaseManifestPath $CompareManifestPath -RunRoot $runRoot -Label $candidate.Label
        Invoke-UnityCapture -ManifestPath $runManifestPath

        Write-Section "Scoring fixtures for $($candidate.Label)"
        $runSummary = Invoke-ScoreRun `
            -ManifestPath $runManifestPath `
            -RunRoot $runRoot `
            -Label $candidate.Label `
            -RulePath $candidate.RulePath `
            -PlannedRulePath $plannedRuleArtifact.executableRulesPath `
            -PlanSummaryPath $plannedRuleArtifact.planSummaryPath `
            -SelectedActions $plannedRuleArtifact.selectedActions

        if (Test-Path $MotionManifestPath)
        {
            Write-Section "Running motion fidelity for $($candidate.Label)"
            $runSummary | Add-Member -NotePropertyName motion -NotePropertyValue (Invoke-MotionSweep -RunRoot $runRoot -Label $candidate.Label -PlannedRulePath $plannedRuleArtifact.executableRulesPath)
        }

        $runSummaries += $runSummary
    }
}
finally
{
    if (-not $NoRestoreDefault)
    {
        Write-Section "Restoring default no-rules generation"
        Invoke-GenerateFixtureSet -RulesPath ""
    }
}

$runSummaries = @(
    $runSummaries |
        Where-Object { $null -ne $_ -and $null -ne $_.PSObject -and $null -ne $_.PSObject.Properties["label"] }
)

$baselineSummary = $runSummaries | Where-Object { $_.label -eq "baseline-no-rules" } | Select-Object -First 1
$rankedSummaries = @(
    $runSummaries |
        Sort-Object -Property averageOverallSimilarityPercent -Descending |
        ForEach-Object {
            $delta = if ($null -ne $baselineSummary)
            {
                [Math]::Round(($_.averageOverallSimilarityPercent - $baselineSummary.averageOverallSimilarityPercent), 4)
            }
            else
            {
                $null
            }

            [pscustomobject]@{
                label = $_.label
                rulePath = $_.rulePath
                plannedRulePath = $_.plannedRulePath
                planSummaryPath = $_.planSummaryPath
                selectedActions = $_.selectedActions
                averageOverallSimilarityPercent = $_.averageOverallSimilarityPercent
                deltaFromBaseline = $delta
                surfaces = $_.surfaces
                motion = $_.motion
            }
        }
)

$leaderboard = [pscustomobject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    compareManifestPath = $CompareManifestPath
    normalization = $Normalization
    tolerance = $Tolerance
    baselineLabel = if ($null -ne $baselineSummary) { $baselineSummary.label } else { $null }
    entries = $rankedSummaries
}

$leaderboardPath = Join-Path $OutputRoot "leaderboard.json"
$leaderboard | ConvertTo-Json -Depth 20 | Set-Content -Path $leaderboardPath

Write-Host ""
Write-Host "Fixture rule sweep complete."
Write-Host "Leaderboard: $leaderboardPath"
