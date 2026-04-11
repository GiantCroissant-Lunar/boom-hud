[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$RuleManifestGlob = "samples/rules/quest-sidebar*.catalog.json",
    [string]$RemotionManifestPath = "fidelity/fixture-remotion.quest-sidebar.json",
    [string]$OutputRoot = "build/fixture-remotion-rule-sweeps/latest",
    [int]$Tolerance = 8,
    [string]$Normalization = "stretch",
    [string[]]$PlanningFacts = @(
        "finding.text-or-icon-metrics-mismatch=present",
        "finding.edge-alignment-mismatch=present"
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
        if ($LASTEXITCODE -ne 0)
        {
            throw "Command '$FilePath $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
        }
    }
    finally
    {
        Pop-Location
    }
}

function Invoke-DotNetCli([string[]]$Arguments)
{
    Invoke-ExternalCommand -WorkingDirectory $RepoRoot -FilePath "dotnet" -Arguments $Arguments
}

function Invoke-RemotionCommand([string[]]$Arguments)
{
    $workingDirectory = Join-Path $RepoRoot "remotion"
    $filePath = $Arguments[0]
    $remainingArguments = if ($Arguments.Length -gt 1) { $Arguments[1..($Arguments.Length - 1)] } else { @() }
    Invoke-ExternalCommand -WorkingDirectory $workingDirectory -FilePath $filePath -Arguments $remainingArguments
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

function Get-ReferencePathForSurface([string]$SurfaceId)
{
    switch ($SurfaceId)
    {
        "party-status-strip" { return Resolve-AbsolutePath "build/fixture-refs/party-status-strip/PSS01.png" }
        "party-status-strip-remotion" { return Resolve-AbsolutePath "build/fixture-refs/party-status-strip/PSS01.png" }
        "quest-sidebar" { return Resolve-AbsolutePath "build/fixture-refs/quest-sidebar/QSB01.png" }
        "quest-sidebar-remotion" { return Resolve-AbsolutePath "build/fixture-refs/quest-sidebar/QSB01.png" }
        "combat-toast-stack" { return Resolve-AbsolutePath "build/fixture-refs/combat-toast-stack/CTS01.png" }
        "combat-toast-stack-remotion" { return Resolve-AbsolutePath "build/fixture-refs/combat-toast-stack/CTS01.png" }
        default { throw "No reference image is configured for surface '$SurfaceId'." }
    }
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

function Invoke-GenerateRemotionFixtureSet([string]$RulesPath)
{
    $remotionOutputPath = Join-Path $RepoRoot "remotion/src/generated"
    $fixtures = @(
        (Resolve-AbsolutePath "samples/pencil/party-status-strip.pen"),
        (Resolve-AbsolutePath "samples/pencil/quest-sidebar.pen"),
        (Resolve-AbsolutePath "samples/pencil/combat-toast-stack.pen")
    )

    foreach ($fixture in $fixtures)
    {
        $args = @(
            "run",
            "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
            "--",
            "generate", $fixture,
            "--target", "remotion",
            "--output", $remotionOutputPath
        )

        if (-not [string]::IsNullOrWhiteSpace($RulesPath))
        {
            $args += @("--rules", $RulesPath)
        }

        Invoke-DotNetCli -Arguments $args
    }
}

function New-RunRemotionManifest(
    [string]$BaseManifestPath,
    [string]$RunRoot,
    [string]$Label)
{
    $manifest = Get-Content $BaseManifestPath -Raw | ConvertFrom-Json -Depth 20
    $manifest.artifactsRoot = $RunRoot

    $manifestsRoot = Join-Path $RunRoot "manifests"
    New-Item -ItemType Directory -Force -Path $manifestsRoot | Out-Null
    $runManifestPath = Join-Path $manifestsRoot "$Label.remotion.json"
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path $runManifestPath
    return $runManifestPath
}

function Invoke-RenderRemotionManifest([string]$ManifestPath)
{
    Invoke-RemotionCommand @(
        "node",
        "--loader", "./node_modules/ts-node/esm.mjs",
        "--experimental-specifier-resolution=node",
        "./render-fidelity.ts",
        "--manifest", $ManifestPath
    )
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

    $surfaceResults = @()
    foreach ($surface in @($manifest.surfaces))
    {
        $surfaceId = [string]$surface.id
        $referencePath = Get-ReferencePathForSurface -SurfaceId $surfaceId
        $candidatePath = Join-Path ([string]$manifest.artifactsRoot) ([string]$surface.remotion.output)
        Wait-ForFileReady -Path $candidatePath
        $reportPath = Join-Path $scoresRoot "$surfaceId.json"
        $diffPath = Join-Path $scoresRoot "$surfaceId.diff.png"

        Invoke-DotNetCli -Arguments @(
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
        ) | Out-Host

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

$RepoRoot = Resolve-AbsolutePath $RepoRoot
$RemotionManifestPath = Resolve-AbsolutePath $RemotionManifestPath
$OutputRoot = Resolve-AbsolutePath $OutputRoot
$ruleSearchPath = if ([System.IO.Path]::IsPathRooted($RuleManifestGlob))
{
    $RuleManifestGlob
}
else
{
    Join-Path $RepoRoot $RuleManifestGlob
}

if (-not (Test-Path $RemotionManifestPath))
{
    throw "Remotion manifest not found: $RemotionManifestPath"
}

$ruleFiles = @(Get-ChildItem -Path $ruleSearchPath -File -ErrorAction SilentlyContinue | Sort-Object FullName)
if ($ruleFiles.Count -eq 0 -and $SkipBaseline)
{
    throw "No rule manifests matched '$ruleSearchPath'."
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

Write-Section "Running Remotion verification"
Invoke-RemotionCommand @("npm", "test")
Invoke-RemotionCommand @("npx", "tsc", "--noEmit")

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

        Write-Section "Generating Remotion fixtures for $($candidate.Label)"
        Invoke-GenerateRemotionFixtureSet -RulesPath $plannedRuleArtifact.executableRulesPath

        Write-Section "Rendering Remotion fixtures for $($candidate.Label)"
        $runManifestPath = New-RunRemotionManifest -BaseManifestPath $RemotionManifestPath -RunRoot $runRoot -Label $candidate.Label
        Invoke-RenderRemotionManifest -ManifestPath $runManifestPath

        $runManifest = Get-Content $runManifestPath -Raw | ConvertFrom-Json -Depth 20
        foreach ($surface in @($runManifest.surfaces))
        {
            $trimProperty = $surface.remotion.PSObject.Properties["trimTransparency"]
            $shouldTrim = ($null -ne $trimProperty) -and [bool]$trimProperty.Value
            if ($shouldTrim)
            {
                Invoke-TrimTransparency -InputPath (Join-Path ([string]$runManifest.artifactsRoot) ([string]$surface.remotion.output))
            }
        }

        Write-Section "Scoring Remotion fixtures for $($candidate.Label)"
        $runSummaries += Invoke-ScoreRun `
            -ManifestPath $runManifestPath `
            -RunRoot $runRoot `
            -Label $candidate.Label `
            -RulePath $candidate.RulePath `
            -PlannedRulePath $plannedRuleArtifact.executableRulesPath `
            -PlanSummaryPath $plannedRuleArtifact.planSummaryPath `
            -SelectedActions $plannedRuleArtifact.selectedActions
    }
}
finally
{
    if (-not $NoRestoreDefault)
    {
        Write-Section "Restoring default no-rules Remotion fixture generation"
        Invoke-GenerateRemotionFixtureSet -RulesPath ""
    }
}

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
            }
        }
)

$leaderboard = [pscustomobject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    remotionManifestPath = $RemotionManifestPath
    normalization = $Normalization
    tolerance = $Tolerance
    baselineLabel = if ($null -ne $baselineSummary) { $baselineSummary.label } else { $null }
    entries = $rankedSummaries
}

$leaderboardPath = Join-Path $OutputRoot "leaderboard.json"
$leaderboard | ConvertTo-Json -Depth 20 | Set-Content -Path $leaderboardPath

Write-Host ""
Write-Host "Remotion fixture rule sweep complete."
Write-Host "Leaderboard: $leaderboardPath"
