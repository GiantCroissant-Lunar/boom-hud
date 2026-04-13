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
    [ValidateSet("strict", "frontier", "cem")]
    [string]$OptimizerMode = "strict",
    [int]$BeamWidth = 5,
    [int]$SearchDepth = 3,
    [int]$ExpansionBudget = 6,
    [int]$CemIterations = 3,
    [int]$CemSampleCount = 8,
    [int]$CemEliteCount = 3,
    [int]$MaxActionsPerSample = 6,
    [ValidateSet("all", "ugui", "unity", "primary", "primary-isolated")]
    [string]$CemFocus = "all",
    [string]$CemWarmStartSummaryPath = "",
    [Nullable[int]]$RandomSeed = $null,
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

function Get-FixtureDocumentKey([object]$Fixture)
{
    $root = [string](Get-OptionalPropertyValue -Source $Fixture -PropertyName "Root")
    if (-not [string]::IsNullOrWhiteSpace($root))
    {
        return $root
    }

    $source = [string](Get-OptionalPropertyValue -Source $Fixture -PropertyName "Source")
    if (-not [string]::IsNullOrWhiteSpace($source))
    {
        return [System.IO.Path]::GetFileNameWithoutExtension($source)
    }

    throw "Fixture document key could not be resolved."
}

function Convert-KebabToPascalCase([string]$Value)
{
    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return ""
    }

    return (
        ($Value -split '[-_\s]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) |
            ForEach-Object {
                if ($_.Length -eq 1)
                {
                    $_.ToUpperInvariant()
                }
                else
                {
                    $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1)
                }
            }
    ) -join ""
}

function Resolve-FixtureFromCompareSurface([object]$Surface)
{
    $unitySpec = Get-OptionalPropertyValue -Source $Surface -PropertyName "unity"
    if ($null -eq $unitySpec)
    {
        return $null
    }

    $sourcePen = [string](Get-OptionalPropertyValue -Source $unitySpec -PropertyName "sourcePen")
    if ([string]::IsNullOrWhiteSpace($sourcePen))
    {
        $surfaceId = [string](Get-OptionalPropertyValue -Source $Surface -PropertyName "id")
        if (-not [string]::IsNullOrWhiteSpace($surfaceId))
        {
            $fixtureStem = $surfaceId -replace '-(ugui|uitk|unity|react|remotion)$', ''
            $candidateSource = Resolve-AbsolutePath ("samples/pencil/{0}.pen" -f $fixtureStem)
            if (Test-Path $candidateSource)
            {
                $sourcePen = $candidateSource
            }
        }
    }
    else
    {
        $sourcePen = Resolve-AbsolutePath $sourcePen
    }

    if ([string]::IsNullOrWhiteSpace($sourcePen) -or -not (Test-Path $sourcePen))
    {
        return $null
    }

    $fixtureRoot = [string](Get-OptionalPropertyValue -Source $unitySpec -PropertyName "generatedRootName")
    if ([string]::IsNullOrWhiteSpace($fixtureRoot))
    {
        $fixtureRoot = Convert-KebabToPascalCase ([System.IO.Path]::GetFileNameWithoutExtension($sourcePen))
    }

    return [pscustomobject]@{
        Source = $sourcePen
        Root = $fixtureRoot
    }
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

    if ($Source -is [System.Collections.IDictionary] -and $Source.Contains($PropertyName))
    {
        return $Source[$PropertyName]
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
        "ruiner-skill-menu-uitk" { return Resolve-AbsolutePath "build/fixture-refs/ruiner-skill-menu/RSM01.png" }
        "ruiner-skill-menu-ugui" { return Resolve-AbsolutePath "build/fixture-refs/ruiner-skill-menu/RSM01.png" }
        "genshin-quests-uitk" { return Resolve-AbsolutePath "build/fixture-refs/genshin-quests/GQS01.png" }
        "genshin-quests-ugui" { return Resolve-AbsolutePath "build/fixture-refs/genshin-quests/GQS01.png" }
        "stardew-journal-uitk" { return Resolve-AbsolutePath "build/fixture-refs/stardew-journal/SDJ01.png" }
        "stardew-journal-ugui" { return Resolve-AbsolutePath "build/fixture-refs/stardew-journal/SDJ01.png" }
        "cyberpunk-crafting-uitk" { return Resolve-AbsolutePath "build/fixture-refs/cyberpunk-crafting/CPC01.png" }
        "cyberpunk-crafting-ugui" { return Resolve-AbsolutePath "build/fixture-refs/cyberpunk-crafting/CPC01.png" }
        "the-alters-crafting-uitk" { return Resolve-AbsolutePath "build/fixture-refs/the-alters-crafting/TAC01.png" }
        "the-alters-crafting-ugui" { return Resolve-AbsolutePath "build/fixture-refs/the-alters-crafting/TAC01.png" }
        "fortnite-inventory-uitk" { return Resolve-AbsolutePath "build/fixture-refs/fortnite-inventory/FTI01.png" }
        "fortnite-inventory-ugui" { return Resolve-AbsolutePath "build/fixture-refs/fortnite-inventory/FTI01.png" }
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
    $manifest = Get-Content $BaseManifestPath -Raw | ConvertFrom-Json
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
    $manifest = Get-Content $BaseManifestPath -Raw | ConvertFrom-Json
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

    $plan = Get-Content $planPath -Raw | ConvertFrom-Json
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
        appliedRules = @($plan.appliedRules)
    }
}

function Invoke-GenerateFixtureSet(
    [string]$RulesPath,
    [hashtable]$UGuiBuildProgramOverrides = @{},
    [bool]$EmitUGuiBuildProgramArtifacts = $false)
{
    $unityOutputPath = Join-Path $UnityProjectPath "Assets/Resources/BoomHudGenerated"
    $uguiOutputPath = Join-Path $UnityProjectPath "Assets/BoomHudGeneratedUGui"
    $fixtures = @()
    $compareManifest = Get-Content $CompareManifestPath -Raw | ConvertFrom-Json
    $fixtureKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($surface in @($compareManifest.surfaces))
    {
        $fixture = Resolve-FixtureFromCompareSurface -Surface $surface
        if ($null -eq $fixture)
        {
            continue
        }

        $fixtureKey = "$($fixture.Source)|$($fixture.Root)"
        if (-not $fixtureKeys.Add($fixtureKey))
        {
            continue
        }

        $fixtures += $fixture
    }

    if ($fixtures.Count -eq 0)
    {
        throw "No fixtures could be resolved from compare manifest '$CompareManifestPath'."
    }

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
        if (-not [string]::IsNullOrWhiteSpace([string]$fixture.Root))
        {
            $unityArgs += @("--root", $fixture.Root)
        }

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
        if (-not [string]::IsNullOrWhiteSpace([string]$fixture.Root))
        {
            $uguiArgs += @("--root", $fixture.Root)
        }

        if ($EmitUGuiBuildProgramArtifacts)
        {
            $uguiArgs += "--emit-ugui-build-program"
        }

        if (-not [string]::IsNullOrWhiteSpace($RulesPath))
        {
            $unityArgs += @("--rules", $RulesPath)
            $uguiArgs += @("--rules", $RulesPath)
        }

        $fixtureDocumentKey = Get-FixtureDocumentKey -Fixture $fixture
        if ($UGuiBuildProgramOverrides.ContainsKey($fixtureDocumentKey))
        {
            $uguiArgs += @("--ugui-build-program", [string]$UGuiBuildProgramOverrides[$fixtureDocumentKey])
        }

        Invoke-DotNetCli -Arguments $unityArgs
        Invoke-DotNetCli -Arguments $uguiArgs
    }
}

function Get-UguiGeneratedArtifactMap()
{
    $uguiOutputPath = Join-Path $UnityProjectPath "Assets/BoomHudGeneratedUGui"
    $map = @{}
    if (-not (Test-Path $uguiOutputPath))
    {
        return $map
    }

    foreach ($buildProgramFile in @(Get-ChildItem -Path $uguiOutputPath -Filter *.ugui-build-program.json -File -ErrorAction SilentlyContinue))
    {
        $documentKey = $buildProgramFile.BaseName -replace '\.ugui-build-program$', ''
        if (-not $map.ContainsKey($documentKey))
        {
            $map[$documentKey] = [ordered]@{
                DocumentKey = $documentKey
                BuildProgramPath = $null
                VisualIrPath = $null
            }
        }

        $map[$documentKey]["BuildProgramPath"] = $buildProgramFile.FullName
    }

    foreach ($visualIrFile in @(Get-ChildItem -Path $uguiOutputPath -Filter *.visual-ir.json -File -ErrorAction SilentlyContinue))
    {
        $documentKey = $visualIrFile.BaseName -replace '\.visual-ir$', ''
        if (-not $map.ContainsKey($documentKey))
        {
            $map[$documentKey] = [ordered]@{
                DocumentKey = $documentKey
                BuildProgramPath = $null
                VisualIrPath = $null
            }
        }

        $map[$documentKey]["VisualIrPath"] = $visualIrFile.FullName
    }

    return $map
}

function Get-ActionStageFromSolveStage([string]$SolveStage)
{
    switch ($SolveStage.ToLowerInvariant())
    {
        "surface" { return 1 }
        "component" { return 1 }
        "motif" { return 2 }
        "atom" { return 3 }
        default { return 2 }
    }
}

function New-UGuiSubtreeActionLibrary([string]$LibraryRoot)
{
    $actions = @()
    $artifactMap = Get-UguiGeneratedArtifactMap
    if ($artifactMap.Count -eq 0)
    {
        return $actions
    }

    New-Item -ItemType Directory -Force -Path $LibraryRoot | Out-Null
    foreach ($entry in @($artifactMap.Values | Sort-Object DocumentKey))
    {
        $buildProgramPath = [string]$entry.BuildProgramPath
        $visualIrPath = [string]$entry.VisualIrPath
        if ([string]::IsNullOrWhiteSpace($buildProgramPath) -or [string]::IsNullOrWhiteSpace($visualIrPath))
        {
            continue
        }

        $documentRoot = Join-Path $LibraryRoot $entry.DocumentKey
        New-Item -ItemType Directory -Force -Path $documentRoot | Out-Null
        $currentBuildProgramPath = $buildProgramPath
        $frontier = @("root")
        $maxScaffoldDepth = 2
        $scaffoldIndex = 0

        for ($depth = 0; $depth -lt $maxScaffoldDepth; $depth++)
        {
            if ($frontier.Count -eq 0)
            {
                break
            }

            $nextFrontier = @()
            foreach ($subtreeStableId in @($frontier | Sort-Object -Unique))
            {
                $scaffoldedBuildProgramPath = Join-Path $documentRoot "$($entry.DocumentKey).scaffold-$scaffoldIndex.ugui-build-program.json"
                $scaffoldReportPath = Join-Path $documentRoot "$($entry.DocumentKey).scaffold-$scaffoldIndex.report.json"
                Invoke-DotNetCli -Arguments @(
                    "run",
                    "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
                    "--",
                    "rules", "scaffold-subtree-candidates",
                    "--visual-ir", $visualIrPath,
                    "--build-program", $currentBuildProgramPath,
                    "--subtree-stable-id", $subtreeStableId,
                    "--out", $scaffoldedBuildProgramPath,
                    "--report-out", $scaffoldReportPath
                ) | Out-Host

                $currentBuildProgramPath = $scaffoldedBuildProgramPath
                $scaffoldReport = Get-Content $scaffoldReportPath -Raw | ConvertFrom-Json
                $nextFrontier += @($scaffoldReport.created | ForEach-Object { [string]$_.stableId })
                $scaffoldIndex += 1
            }

            $frontier = @($nextFrontier | Sort-Object -Unique)
        }

        $scaffoldedBuildProgram = Get-Content $currentBuildProgramPath -Raw | ConvertFrom-Json
        $acceptedByStableId = @{}
        foreach ($selection in @($scaffoldedBuildProgram.acceptedCandidates))
        {
            $acceptedByStableId[[string]$selection.stableId] = [string]$selection.candidateId
        }

        foreach ($catalog in @($scaffoldedBuildProgram.candidateCatalogs))
        {
            $stableId = [string]$catalog.stableId
            $baselineCandidateId = if ($acceptedByStableId.ContainsKey($stableId))
            {
                [string]$acceptedByStableId[$stableId]
            }
            else
            {
                [string]$catalog.candidates[0].candidateId
            }

            foreach ($candidate in @($catalog.candidates))
            {
                $candidateId = [string]$candidate.candidateId
                if ([string]::Equals($candidateId, $baselineCandidateId, [System.StringComparison]::Ordinal))
                {
                    continue
                }

                $actions += [pscustomobject]@{
                    Id = "subtree-ugui-$($entry.DocumentKey)-$stableId-$candidateId"
                    Label = "subtree ugui $($entry.DocumentKey) $stableId -> $candidateId"
                    Stage = Get-ActionStageFromSolveStage -SolveStage ([string]$catalog.solveStage)
                    UGuiBuildProgramSelection = [ordered]@{
                        documentKey = [string]$entry.DocumentKey
                        buildProgramPath = $currentBuildProgramPath
                        stableId = $stableId
                        candidateId = $candidateId
                    }
                }
            }
        }
    }

    return @(
        $actions |
            Sort-Object @{ Expression = "Stage"; Ascending = $true }, @{ Expression = "Label"; Ascending = $true }
    )
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
    $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
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

        $report = Get-Content $reportPath -Raw | ConvertFrom-Json
        $surfaceResults += [pscustomobject]@{
            id = $surfaceId
            referencePath = $referencePath
            candidatePath = $candidatePath
            reportPath = $reportPath
            diffPath = $diffPath
            measuredLayoutPath = if (Test-Path (Join-Path $scoresRoot "$surfaceId.measured-layout.json")) { (Join-Path $scoresRoot "$surfaceId.measured-layout.json") } else { $null }
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
    $summary | Add-Member -NotePropertyName summaryPath -NotePropertyValue $summaryPath
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

    $motionEntries = @(Get-Content $motionSummaryPath -Raw | ConvertFrom-Json)
    $motionScorableEntries = @($motionEntries | Where-Object {
        $_ -and
        $_.PSObject -and
        ($_.PSObject.Properties.Name -contains "overallSimilarityPercent") -and
        $null -ne $_.overallSimilarityPercent
    })
    $average = if ($motionScorableEntries.Count -gt 0)
    {
        [Math]::Round((($motionScorableEntries | Measure-Object -Property overallSimilarityPercent -Average).Average), 4)
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

function New-RuleRecordFromPlanRule([object]$PlannedRule)
{
    $phase = Get-OptionalPropertyValue -Source $PlannedRule -PropertyName "phase"
    return [ordered]@{
        name = [string]$PlannedRule.name
        phase = if ($null -eq $phase -or [string]::IsNullOrWhiteSpace([string]$phase)) { $null } else { [string]$phase }
        cost = [double]$PlannedRule.cost
        preconditions = @($PlannedRule.preconditions)
        effects = @($PlannedRule.effects)
        selector = $PlannedRule.selector
        action = $PlannedRule.action
    }
}

function Get-ActionStageFromRule([object]$Rule)
{
    $phase = [string](Get-OptionalPropertyValue -Source $Rule -PropertyName "phase")
    if ([string]::Equals($phase, "structure", [System.StringComparison]::OrdinalIgnoreCase))
    {
        return 1
    }

    if ([string]::Equals($phase, "text", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($phase, "icon", [System.StringComparison]::OrdinalIgnoreCase))
    {
        return 3
    }

    $action = Get-OptionalPropertyValue -Source $Rule -PropertyName "action"
    $layout = Get-OptionalPropertyValue -Source $action -PropertyName "layout"
    if ($null -ne $layout)
    {
        $hasOffset = $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "offsetX") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "offsetXDelta") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "offsetY") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "offsetYDelta") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetTop") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetTopDelta") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetRight") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetRightDelta") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetBottom") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetBottomDelta") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetLeft") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "insetLeftDelta")
        $hasInnerLayout = $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "gap") `
            -or $null -ne (Get-OptionalPropertyValue -Source $layout -PropertyName "gapDelta") `
            -or $hasOffset
        if ($hasInnerLayout)
        {
            return 2
        }
    }

    return 1
}

function New-TemplateRule(
    [string]$Name,
    [string]$Phase,
    [string]$Backend,
    [string]$ComponentType,
    [string]$TemplateKind,
    [double]$NumberValue,
    [object]$BoolValue,
    [hashtable]$AdditionalSelector = @{},
    [hashtable]$TemplateParameters = @{})
{
    $selector = [ordered]@{
        backend = $Backend
        componentType = $ComponentType
    }

    foreach ($key in $AdditionalSelector.Keys)
    {
        $selector[$key] = $AdditionalSelector[$key]
    }

    $template = [ordered]@{
        kind = $TemplateKind
    }
    if ($NumberValue -ne [double]::MinValue)
    {
        $template["numberValue"] = $NumberValue
    }

    if ($null -ne $BoolValue)
    {
        $template["boolValue"] = [bool]$BoolValue
    }

    if ($TemplateParameters.Count -gt 0)
    {
        $template["parameters"] = $TemplateParameters
    }

    return [ordered]@{
        name = $Name
        phase = $Phase
        cost = 1.0
        selector = $selector
        template = $template
    }
}

function New-TemplateMetricProfile(
    [string]$Name,
    [string]$Backend,
    [string]$ComponentType = "",
    [string]$TemplateKind,
    [double]$NumberValue,
    [object]$BoolValue,
    [hashtable]$AdditionalSelector = @{},
    [hashtable]$TemplateParameters = @{})
{
    $selector = [ordered]@{}

    if (-not [string]::IsNullOrWhiteSpace($Backend))
    {
        $selector["backend"] = $Backend
    }

    if (-not [string]::IsNullOrWhiteSpace($ComponentType))
    {
        $selector["componentType"] = $ComponentType
    }

    foreach ($key in $AdditionalSelector.Keys)
    {
        $selector[$key] = $AdditionalSelector[$key]
    }

    $template = [ordered]@{
        kind = $TemplateKind
    }
    if ($NumberValue -ne [double]::MinValue)
    {
        $template["numberValue"] = $NumberValue
    }

    if ($null -ne $BoolValue)
    {
        $template["boolValue"] = [bool]$BoolValue
    }

    if ($TemplateParameters.Count -gt 0)
    {
        $template["parameters"] = $TemplateParameters
    }

    return [ordered]@{
        name = $Name
        selector = $selector
        template = $template
    }
}

function New-MetricVariantActionLibrary()
{
    $actions = @()
    $backends = @("unity", "ugui")
    $textSizeBands = @("xsmall", "small", "medium", "large", "xlarge")
    $iconSizeBands = @("small", "medium", "large", "xlarge")
    $semanticTextBuckets = @(
        @{ selector = @{ semanticClass = "heading-label" }; label = "heading"; componentType = "label"; variants = @("font", "line", "letter", "wrap") },
        @{ selector = @{ semanticClass = "stacked-text-line" }; label = "stacked-line"; componentType = "label"; variants = @("font", "line", "letter", "wrap") },
        @{ selector = @{ semanticClass = "pixel-text" }; label = "pixel"; componentType = ""; variants = @("font", "line", "letter", "wrap") }
    )
    $focusedPixelBuckets = @(
        @{
            selector = @{ semanticClass = "pixel-text"; sizeBand = "xsmall"; wrapText = $true; textGrowth = "fixed-width" }
            label = "pixel-xsmall-fixedwrap"
            componentType = ""
            variants = @("font", "line", "letter", "wrap")
            textGrowthValues = @()
        },
        @{
            selector = @{ semanticClass = "pixel-text"; sizeBand = "xsmall"; wrapText = $false }
            label = "pixel-xsmall-nowrap"
            componentType = ""
            variants = @("font", "line", "letter", "wrap")
            textGrowthValues = @("fixed-width")
        },
        @{
            selector = @{ semanticClass = "pixel-text"; sizeBand = "small"; wrapText = $false }
            label = "pixel-small-nowrap"
            componentType = ""
            variants = @("font", "line", "letter", "wrap")
            textGrowthValues = @("fixed-width")
        },
        @{
            selector = @{ semanticClass = "pixel-text"; sizeBand = "medium"; wrapText = $true; textGrowth = "fixed-width" }
            label = "pixel-medium-fixedwrap"
            componentType = ""
            variants = @("font", "line", "letter", "wrap")
            textGrowthValues = @()
        },
        @{
            selector = @{ semanticClass = "pixel-text"; sizeBand = "medium"; wrapText = $false }
            label = "pixel-medium-nowrap"
            componentType = ""
            variants = @("font", "line", "letter", "wrap")
            textGrowthValues = @("fixed-width")
        },
        @{
            selector = @{ semanticClass = "pixel-text"; sizeBand = "large"; wrapText = $false }
            label = "pixel-large-nowrap"
            componentType = ""
            variants = @("font", "line", "letter", "wrap")
            textGrowthValues = @("fixed-width")
        }
    )

    function New-MetricProfileActionRecord(
        [string]$Id,
        [string]$Label,
        [string]$Backend,
        [string]$ComponentType,
        [string]$TemplateKind,
        [double]$NumberValue,
        [object]$BoolValue,
        [hashtable]$Selector = @{},
        [hashtable]$TemplateParameters = @{})
    {
        return [pscustomobject]@{
            Id = $Id
            Label = $Label
            Stage = 3
            MetricProfile = (New-TemplateMetricProfile -Name $Id -Backend $Backend -ComponentType $ComponentType -TemplateKind $TemplateKind -NumberValue $NumberValue -BoolValue $BoolValue -AdditionalSelector $Selector -TemplateParameters $TemplateParameters)
        }
    }

    foreach ($backend in $backends)
    {
        foreach ($sizeBand in $textSizeBands)
        {
            $selector = @{ semanticClass = "pixel-text"; sizeBand = $sizeBand }
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-font-minus-1" -Label "$backend pixel $sizeBand font -1" -Backend $backend -ComponentType "" -TemplateKind "fontSizeDelta" -NumberValue -1.0 -BoolValue $null -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-font-plus-1" -Label "$backend pixel $sizeBand font +1" -Backend $backend -ComponentType "" -TemplateKind "fontSizeDelta" -NumberValue 1.0 -BoolValue $null -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-line-tight" -Label "$backend pixel $sizeBand line 0.95" -Backend $backend -ComponentType "" -TemplateKind "lineHeightMode" -NumberValue 0.95 -BoolValue $null -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-line-loose" -Label "$backend pixel $sizeBand line 1.05" -Backend $backend -ComponentType "" -TemplateKind "lineHeightMode" -NumberValue 1.05 -BoolValue $null -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-letter-tight" -Label "$backend pixel $sizeBand letter -0.5" -Backend $backend -ComponentType "" -TemplateKind "letterSpacingDelta" -NumberValue -0.5 -BoolValue $null -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-letter-loose" -Label "$backend pixel $sizeBand letter +0.5" -Backend $backend -ComponentType "" -TemplateKind "letterSpacingDelta" -NumberValue 0.5 -BoolValue $null -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-wrap-tight" -Label "$backend pixel $sizeBand wrap tight" -Backend $backend -ComponentType "" -TemplateKind "wrapPolicy" -NumberValue ([double]::MinValue) -BoolValue $false -Selector $selector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-pixel-$sizeBand-wrap-loose" -Label "$backend pixel $sizeBand wrap loose" -Backend $backend -ComponentType "" -TemplateKind "wrapPolicy" -NumberValue ([double]::MinValue) -BoolValue $true -Selector $selector
        }

        foreach ($bucket in $semanticTextBuckets)
        {
            if ($bucket.variants -contains "font")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-font-minus-1" -Label "$backend $($bucket.label) font -1" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "fontSizeDelta" -NumberValue -1.0 -BoolValue $null -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-font-plus-1" -Label "$backend $($bucket.label) font +1" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "fontSizeDelta" -NumberValue 1.0 -BoolValue $null -Selector $bucket.selector
            }

            if ($bucket.variants -contains "line")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-line-tight" -Label "$backend $($bucket.label) line 0.95" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "lineHeightMode" -NumberValue 0.95 -BoolValue $null -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-line-loose" -Label "$backend $($bucket.label) line 1.05" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "lineHeightMode" -NumberValue 1.05 -BoolValue $null -Selector $bucket.selector
            }

            if ($bucket.variants -contains "letter")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-letter-tight" -Label "$backend $($bucket.label) letter -0.5" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "letterSpacingDelta" -NumberValue -0.5 -BoolValue $null -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-letter-loose" -Label "$backend $($bucket.label) letter +0.5" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "letterSpacingDelta" -NumberValue 0.5 -BoolValue $null -Selector $bucket.selector
            }

            if ($bucket.variants -contains "wrap")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-wrap-tight" -Label "$backend $($bucket.label) wrap tight" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "wrapPolicy" -NumberValue ([double]::MinValue) -BoolValue $false -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-wrap-loose" -Label "$backend $($bucket.label) wrap loose" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "wrapPolicy" -NumberValue ([double]::MinValue) -BoolValue $true -Selector $bucket.selector
            }
        }

        foreach ($bucket in $focusedPixelBuckets)
        {
            if ($bucket.variants -contains "font")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-font-minus-1" -Label "$backend $($bucket.label) font -1" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "fontSizeDelta" -NumberValue -1.0 -BoolValue $null -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-font-plus-1" -Label "$backend $($bucket.label) font +1" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "fontSizeDelta" -NumberValue 1.0 -BoolValue $null -Selector $bucket.selector
            }

            if ($bucket.variants -contains "line")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-line-tight" -Label "$backend $($bucket.label) line 0.95" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "lineHeightMode" -NumberValue 0.95 -BoolValue $null -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-line-loose" -Label "$backend $($bucket.label) line 1.05" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "lineHeightMode" -NumberValue 1.05 -BoolValue $null -Selector $bucket.selector
            }

            if ($bucket.variants -contains "letter")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-letter-tight" -Label "$backend $($bucket.label) letter -0.5" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "letterSpacingDelta" -NumberValue -0.5 -BoolValue $null -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-letter-loose" -Label "$backend $($bucket.label) letter +0.5" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "letterSpacingDelta" -NumberValue 0.5 -BoolValue $null -Selector $bucket.selector
            }

            if ($bucket.variants -contains "wrap")
            {
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-wrap-tight" -Label "$backend $($bucket.label) wrap tight" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "wrapPolicy" -NumberValue ([double]::MinValue) -BoolValue $false -Selector $bucket.selector
                $actions += New-MetricProfileActionRecord -Id "metric-$backend-$($bucket.label)-wrap-loose" -Label "$backend $($bucket.label) wrap loose" -Backend $backend -ComponentType $bucket.componentType -TemplateKind "wrapPolicy" -NumberValue ([double]::MinValue) -BoolValue $true -Selector $bucket.selector
            }

            foreach ($textGrowthValue in @($bucket.textGrowthValues))
            {
                if ([string]::IsNullOrWhiteSpace([string]$textGrowthValue))
                {
                    continue
                }

                $textGrowthActionArgs = @{
                    Id = "metric-$backend-$($bucket.label)-textgrowth-$($textGrowthValue -replace '[^a-zA-Z0-9]+', '-')"
                    Label = "$backend $($bucket.label) text growth $textGrowthValue"
                    Backend = $backend
                    ComponentType = $bucket.componentType
                    TemplateKind = "textGrowthPolicy"
                    NumberValue = [double]::MinValue
                    BoolValue = $null
                    Selector = $bucket.selector
                    TemplateParameters = @{ textGrowth = $textGrowthValue }
                }
                $actions += New-MetricProfileActionRecord @textGrowthActionArgs
            }
        }

        foreach ($sizeBand in $iconSizeBands)
        {
            $iconSelector = @{ semanticClass = "icon-glyph"; sizeBand = $sizeBand }
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-icon-$sizeBand-font-minus-1" -Label "$backend icon $sizeBand font -1" -Backend $backend -ComponentType "icon" -TemplateKind "iconFontSizeDelta" -NumberValue -1.0 -BoolValue $null -Selector $iconSelector
            $actions += New-MetricProfileActionRecord -Id "metric-$backend-icon-$sizeBand-font-plus-1" -Label "$backend icon $sizeBand font +1" -Backend $backend -ComponentType "icon" -TemplateKind "iconFontSizeDelta" -NumberValue 1.0 -BoolValue $null -Selector $iconSelector
        }

        $actions += New-MetricProfileActionRecord -Id "metric-$backend-icon-baseline-minus-1" -Label "$backend icon baseline -1" -Backend $backend -ComponentType "icon" -TemplateKind "iconBaselineOffsetDelta" -NumberValue -1.0 -BoolValue $null -Selector @{ semanticClass = "icon-glyph" }
        $actions += New-MetricProfileActionRecord -Id "metric-$backend-icon-baseline-plus-1" -Label "$backend icon baseline +1" -Backend $backend -ComponentType "icon" -TemplateKind "iconBaselineOffsetDelta" -NumberValue 1.0 -BoolValue $null -Selector @{ semanticClass = "icon-glyph" }
        $actions += New-MetricProfileActionRecord -Id "metric-$backend-icon-centering-off" -Label "$backend icon centering off" -Backend $backend -ComponentType "icon" -TemplateKind "iconCenteringPolicy" -NumberValue ([double]::MinValue) -BoolValue $false -Selector @{ semanticClass = "icon-glyph" }
        $actions += New-MetricProfileActionRecord -Id "metric-$backend-icon-centering-on" -Label "$backend icon centering on" -Backend $backend -ComponentType "icon" -TemplateKind "iconCenteringPolicy" -NumberValue ([double]::MinValue) -BoolValue $true -Selector @{ semanticClass = "icon-glyph" }
    }

    return $actions
}

function New-FrontierActionLibrary([System.IO.FileInfo[]]$RuleFiles)
{
    $actions = @()
    $libraryRoot = Join-Path $OutputRoot "_frontier-library"
    New-Item -ItemType Directory -Force -Path $libraryRoot | Out-Null
    foreach ($ruleFile in $RuleFiles)
    {
        $runRoot = Join-Path $libraryRoot $ruleFile.BaseName
        New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
        $planned = New-PlannedRuleArtifact -RulePath $ruleFile.FullName -RunRoot $runRoot -Label $ruleFile.BaseName
        foreach ($plannedRule in @($planned.appliedRules))
        {
            if ([string]::IsNullOrWhiteSpace([string]$plannedRule.name))
            {
                continue
            }

            $actions += [pscustomobject]@{
                Id = "catalog-$($ruleFile.BaseName)-$([string]$plannedRule.name)"
                Label = [string]$plannedRule.name
                Stage = Get-ActionStageFromRule -Rule $plannedRule
                Rule = (New-RuleRecordFromPlanRule -PlannedRule $plannedRule)
            }
        }
    }

    $actions += New-MetricVariantActionLibrary
    return @(
        $actions |
            Sort-Object @{ Expression = "Stage"; Ascending = $true }, @{ Expression = "Label"; Ascending = $true }
    )
}

function Get-CemMetricGroupKey([object]$MetricProfile)
{
    $selector = Get-OptionalPropertyValue -Source $MetricProfile -PropertyName "selector"
    $template = Get-OptionalPropertyValue -Source $MetricProfile -PropertyName "template"
    if ($null -eq $selector -or $null -eq $template)
    {
        throw "CEM metric group key requires selector and template metadata."
    }

    $selectorParts = @()
    $selectorKeys = if ($selector -is [System.Collections.IDictionary])
    {
        @($selector.Keys | Sort-Object)
    }
    else
    {
        @($selector.PSObject.Properties.Name | Sort-Object)
    }

    foreach ($key in @($selectorKeys))
    {
        $value = if ($selector -is [System.Collections.IDictionary]) { $selector[$key] } else { $selector.$key }
        $selectorParts += "$key=$([string]$value)"
    }

    $templateKind = if ($template -is [System.Collections.IDictionary])
    {
        [string]$template["kind"]
    }
    else
    {
        [string]$template.kind
    }

    return "$templateKind|$($selectorParts -join ';')"
}

function Get-CemActionGroupKey([object]$Action)
{
    $metricProfile = Get-OptionalPropertyValue -Source $Action -PropertyName "MetricProfile"
    if ($null -ne $metricProfile)
    {
        return Get-CemMetricGroupKey -MetricProfile $metricProfile
    }

    $subtreeSelection = Get-OptionalPropertyValue -Source $Action -PropertyName "UGuiBuildProgramSelection"
    if ($null -ne $subtreeSelection)
    {
        $documentKey = [string](Get-OptionalPropertyValue -Source $subtreeSelection -PropertyName "documentKey")
        $stableId = [string](Get-OptionalPropertyValue -Source $subtreeSelection -PropertyName "stableId")
        return "uguiSubtree|document=$documentKey;stableId=$stableId"
    }

    throw "CEM action group key requires metric-profile or subtree-selection metadata."
}

function Get-PrimaryDocumentKey([string]$PrimarySurfaceId)
{
    if ([string]::IsNullOrWhiteSpace($PrimarySurfaceId))
    {
        return ""
    }

    $fixtureStem = $PrimarySurfaceId -replace '-(ugui|uitk|unity|react|remotion)$', ''
    if ([string]::IsNullOrWhiteSpace($fixtureStem))
    {
        return ""
    }

    return Convert-KebabToPascalCase $fixtureStem
}

function New-CemActionGroups([object[]]$ActionLibrary)
{
    $groupableActions = @(
        $ActionLibrary |
            Where-Object {
                $null -ne (Get-OptionalPropertyValue -Source $_ -PropertyName "MetricProfile") `
                    -or $null -ne (Get-OptionalPropertyValue -Source $_ -PropertyName "UGuiBuildProgramSelection")
            } |
            Sort-Object @{ Expression = "Stage"; Ascending = $true }, @{ Expression = "Id"; Ascending = $true }
    )

    if ($groupableActions.Count -eq 0)
    {
        throw "CEM optimizer requires metric-profile or subtree-selection actions."
    }

    $groups = @()
    $groupIndex = 0
    foreach ($group in @($groupableActions | Group-Object { Get-CemActionGroupKey -Action $_ }))
    {
        $first = $group.Group[0]
        $groups += [pscustomobject]@{
            GroupId = "metric-group-$groupIndex"
            Key = [string]$group.Name
            Stage = [int]$first.Stage
            ActionIds = @($group.Group | Sort-Object Id | ForEach-Object { [string]$_.Id })
        }
        $groupIndex += 1
    }

    return @(
        $groups |
            Sort-Object @{ Expression = "Stage"; Ascending = $true }, @{ Expression = "Key"; Ascending = $true }
    )
}

function Test-CemGroupMatchesFocus(
    [object]$Group,
    [string]$Focus,
    [string]$PrimaryDocumentKey = "")
{
    function Test-IsPrimaryIsolatedMetricGroup([string]$Key)
    {
        if ($Key -notlike "*backend=ugui*" -or $Key -notlike "*semanticClass=pixel-text*")
        {
            return $false
        }

        return (
            $Key -like "*sizeBand=xsmall;textGrowth=fixed-width;wrapText=True*" -or
            $Key -like "*sizeBand=xsmall;wrapText=False*" -or
            $Key -like "*sizeBand=small;wrapText=False*" -or
            $Key -like "*sizeBand=medium;textGrowth=fixed-width;wrapText=True*" -or
            $Key -like "*sizeBand=medium;wrapText=False*" -or
            $Key -like "*sizeBand=large;wrapText=False*"
        )
    }

    switch ($Focus)
    {
        "primary-isolated" {
            return Test-IsPrimaryIsolatedMetricGroup -Key ([string]$Group.Key)
        }
        "primary" {
            return (
                ([string]$Group.Key) -like "*backend=ugui*" -or
                (
                    -not [string]::IsNullOrWhiteSpace($PrimaryDocumentKey) -and
                    ([string]$Group.Key) -like "uguiSubtree|document=$PrimaryDocumentKey;*"
                )
            )
        }
        "ugui" { return ([string]$Group.Key) -like "*backend=ugui*" -or ([string]$Group.Key) -like "uguiSubtree|*" }
        "unity" { return ([string]$Group.Key) -like "*backend=unity*" }
        default { return $true }
    }
}

function New-CemDistributions(
    [object[]]$Groups,
    [string]$PrimaryDocumentKey = "")
{
    $distributions = @()
    foreach ($group in @($Groups))
    {
        $options = @("__none__") + @($group.ActionIds)
        $initialNoneProbability = 0.85
        if (
            -not [string]::IsNullOrWhiteSpace($PrimaryDocumentKey) -and
            ([string]$group.Key) -like "uguiSubtree|document=$PrimaryDocumentKey;*"
        )
        {
            $initialNoneProbability = if ([int]$group.Stage -le 1) { 0.6 } else { 0.7 }
        }

        $actionProbability = if ($group.ActionIds.Count -gt 0)
        {
            (1.0 - $initialNoneProbability) / [double]$group.ActionIds.Count
        }
        else
        {
            0.0
        }

        $probabilities = [ordered]@{}
        foreach ($option in $options)
        {
            $probabilities[$option] = if ($option -eq "__none__") { $initialNoneProbability } else { $actionProbability }
        }

        $distributions += [pscustomobject]@{
            GroupId = $group.GroupId
            Key = $group.Key
            Stage = $group.Stage
            ActionIds = @($group.ActionIds)
            Options = @($options)
            Probabilities = $probabilities
        }
    }

    return @($distributions)
}

function Normalize-CemProbabilities([System.Collections.Specialized.OrderedDictionary]$Probabilities, [string[]]$Options)
{
    $total = 0.0
    foreach ($option in @($Options))
    {
        $total += [double]$Probabilities[$option]
    }

    if ($total -le 0.0)
    {
        $uniform = 1.0 / [double]([Math]::Max(1, $Options.Count))
        foreach ($option in @($Options))
        {
            $Probabilities[$option] = $uniform
        }
        return
    }

    foreach ($option in @($Options))
    {
        $Probabilities[$option] = [double]$Probabilities[$option] / $total
    }
}

function Get-CemSampledOption([object]$Distribution, [System.Random]$Random)
{
    $threshold = $Random.NextDouble()
    $running = 0.0
    foreach ($option in @($Distribution.Options))
    {
        $running += [double]$Distribution.Probabilities[$option]
        if ($threshold -le $running)
        {
            return [string]$option
        }
    }

    return [string]$Distribution.Options[-1]
}

function Get-CemSampledActionIds(
    [object[]]$Distributions,
    [System.Random]$Random,
    [int]$MaxActions)
{
    $actionIds = @()
    foreach ($distribution in @($Distributions))
    {
        $option = Get-CemSampledOption -Distribution $distribution -Random $Random
        if ($option -ne "__none__")
        {
            $actionIds += $option
        }
    }

    if ($actionIds.Count -gt $MaxActions)
    {
        $actionIds = @(
            $actionIds |
                Sort-Object { $Random.Next() } |
                Select-Object -First $MaxActions
        )
    }

    return @($actionIds | Sort-Object -Unique)
}

function Update-CemDistributions(
    [object[]]$Distributions,
    [object[]]$EliteCandidates,
    [double]$Smoothing = 0.6)
{
    if ($EliteCandidates.Count -eq 0)
    {
        return
    }

    foreach ($distribution in @($Distributions))
    {
        $counts = @{}
        foreach ($option in @($distribution.Options))
        {
            $counts[$option] = 0.0
        }

        foreach ($candidate in @($EliteCandidates))
        {
            $selectedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($actionId in @($candidate.AppliedActionIds))
            {
                $null = $selectedSet.Add([string]$actionId)
            }

            $selectedOption = "__none__"
            foreach ($actionId in @($distribution.ActionIds))
            {
                if ($selectedSet.Contains([string]$actionId))
                {
                    $selectedOption = [string]$actionId
                    break
                }
            }

            $counts[$selectedOption] = [double]$counts[$selectedOption] + 1.0
        }

        foreach ($option in @($distribution.Options))
        {
            $targetProbability = [double]$counts[$option] / [double]$EliteCandidates.Count
            $currentProbability = [double]$distribution.Probabilities[$option]
            $distribution.Probabilities[$option] = ((1.0 - $Smoothing) * $currentProbability) + ($Smoothing * $targetProbability)
        }

        Normalize-CemProbabilities -Probabilities $distribution.Probabilities -Options $distribution.Options
    }
}

function Get-CemWarmStartCandidates(
    [string]$SummaryPath,
    [int]$MaxCount)
{
    if ([string]::IsNullOrWhiteSpace($SummaryPath) -or -not (Test-Path $SummaryPath))
    {
        return @()
    }

    $summary = Get-Content $SummaryPath -Raw | ConvertFrom-Json
    $candidates = @($summary.Candidates)
    if ($candidates.Count -eq 0)
    {
        return @()
    }

    $selectedCandidateId = [string]$summary.SelectedCandidateId
    $ordered = @(
        $candidates |
            Where-Object { [bool]$_.GuardResult.Passed } |
            Sort-Object `
                @{ Expression = { if ([string]$_.CandidateId -eq $selectedCandidateId) { 0 } else { 1 } }; Ascending = $true }, `
                @{ Expression = "Rank"; Ascending = $true }, `
                @{ Expression = "CandidateId"; Ascending = $true }
    )

    $result = @()
    foreach ($candidate in @($ordered | Select-Object -First ([Math]::Max(1, $MaxCount))))
    {
        $result += [pscustomobject]@{
            CandidateId = [string]$candidate.CandidateId
            AppliedActionIds = @($candidate.AppliedActions | ForEach-Object { [string]$_ })
        }
    }

    return @($result)
}

function Write-CemIterationSnapshot(
    [string]$Path,
    [int]$Iteration,
    [object[]]$Distributions,
    [object[]]$EliteEvaluations)
{
    $snapshot = [ordered]@{
        iteration = $Iteration
        distributions = @(
            $Distributions |
                ForEach-Object {
                    [ordered]@{
                        groupId = $_.GroupId
                        key = $_.Key
                        stage = $_.Stage
                        probabilities = $_.Probabilities
                    }
                }
        )
        elites = @(
            $EliteEvaluations |
                ForEach-Object {
                    [ordered]@{
                        candidateId = $_.candidateId
                        rank = $_.rank
                        appliedActions = @($_.appliedActions)
                    }
                }
        )
    }

    $snapshot | ConvertTo-Json -Depth 20 | Set-Content -Path $Path
}

function Write-FrontierRuleSet(
    [string]$OutputPath,
    [hashtable]$ActionMap,
    [string[]]$ActionIds)
{
    $rules = @()
    $metricProfiles = @()
    foreach ($actionId in @($ActionIds))
    {
        if (-not $ActionMap.ContainsKey($actionId))
        {
            throw "Unknown frontier action '$actionId'."
        }

        $action = $ActionMap[$actionId]
        $rule = Get-OptionalPropertyValue -Source $action -PropertyName "Rule"
        if ($null -ne $rule)
        {
            $rules += $rule
        }

        $metricProfile = Get-OptionalPropertyValue -Source $action -PropertyName "MetricProfile"
        if ($null -ne $metricProfile)
        {
            $metricProfiles += $metricProfile
        }
    }

    $ruleSet = [ordered]@{
        version = "1.0"
        metricProfiles = $metricProfiles
        rules = $rules
    }

    $directory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($directory))
    {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $ruleSet | ConvertTo-Json -Depth 20 | Set-Content -Path $OutputPath
}

function New-CandidateUGuiBuildProgramOverrides(
    [string]$CandidateId,
    [hashtable]$ActionMap,
    [string[]]$ActionIds)
{
    $selectionGroups = @{}
    foreach ($actionId in @($ActionIds))
    {
        if (-not $ActionMap.ContainsKey($actionId))
        {
            continue
        }

        $selection = Get-OptionalPropertyValue -Source $ActionMap[$actionId] -PropertyName "UGuiBuildProgramSelection"
        if ($null -eq $selection)
        {
            continue
        }

        $documentKey = [string](Get-OptionalPropertyValue -Source $selection -PropertyName "documentKey")
        if ([string]::IsNullOrWhiteSpace($documentKey))
        {
            continue
        }

        if (-not $selectionGroups.ContainsKey($documentKey))
        {
            $selectionGroups[$documentKey] = @()
        }

        $selectionGroups[$documentKey] += $selection
    }

    if ($selectionGroups.Count -eq 0)
    {
        return @{}
    }

    $overridesRoot = Join-Path $OutputRoot "_frontier-build-programs"
    New-Item -ItemType Directory -Force -Path $overridesRoot | Out-Null
    $overrideMap = @{}
    foreach ($documentKey in @($selectionGroups.Keys | Sort-Object))
    {
        $selections = @($selectionGroups[$documentKey])
        $baseBuildProgramPath = [string](Get-OptionalPropertyValue -Source $selections[0] -PropertyName "buildProgramPath")
        $buildProgram = Get-Content $baseBuildProgramPath -Raw | ConvertFrom-Json
        $accepted = @($buildProgram.acceptedCandidates)

        foreach ($selection in $selections)
        {
            $stableId = [string](Get-OptionalPropertyValue -Source $selection -PropertyName "stableId")
            $candidateId = [string](Get-OptionalPropertyValue -Source $selection -PropertyName "candidateId")
            $accepted = @($accepted | Where-Object { [string]$_.stableId -ne $stableId })
            $accepted += [pscustomobject]@{
                stableId = $stableId
                candidateId = $candidateId
            }
        }

        $buildProgram.acceptedCandidates = $accepted
        $overridePath = Join-Path $overridesRoot "$CandidateId.$documentKey.ugui-build-program.json"
        $buildProgram | ConvertTo-Json -Depth 40 | Set-Content -Path $overridePath
        $overrideMap[$documentKey] = $overridePath
    }

    return $overrideMap
}

function Write-FrontierOptimizerState(
    [string]$Path,
    [string]$PrimarySurfaceId,
    [string]$BaselineCandidateId,
    [object[]]$Candidates)
{
    $state = [ordered]@{
        optimizerMode = $OptimizerMode
        beamWidth = $BeamWidth
        searchDepth = $SearchDepth
        expansionBudget = $ExpansionBudget
        primarySurfaceId = $PrimarySurfaceId
        baselineCandidateId = $BaselineCandidateId
        candidates = @(
            $Candidates |
                ForEach-Object {
                    $summaryPath = Get-OptionalPropertyValue -Source $_ -PropertyName "RunSummaryPath"
                    if ([string]::IsNullOrWhiteSpace([string]$summaryPath))
                    {
                        $summaryPath = Get-OptionalPropertyValue -Source $_.RunSummary -PropertyName "summaryPath"
                    }
                    if ([string]::IsNullOrWhiteSpace([string]$summaryPath))
                    {
                        $summaryPath = Join-Path (Join-Path $OutputRoot $_.CandidateId) "summary.json"
                    }
                    [ordered]@{
                        candidateId = $_.CandidateId
                        label = $_.Label
                        summaryPath = $summaryPath
                        parentCandidateId = $_.ParentCandidateId
                        depth = $_.Depth
                        appliedActions = @($_.AppliedActionIds)
                    }
                }
        )
    }

    $state | ConvertTo-Json -Depth 20 | Set-Content -Path $Path
}

function Invoke-FrontierOptimizer(
    [string]$StatePath,
    [string]$SummaryPath)
{
    $args = @(
        "run",
        "--project", "dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj",
        "--",
        "rules", "frontier-optimize",
        "--input", $StatePath,
        "--out", $SummaryPath,
        "--summary", "false"
    )

    Invoke-DotNetCli -Arguments $args | Out-Host
    return (Get-Content $SummaryPath -Raw | ConvertFrom-Json)
}

function Get-PrimarySurfaceId([string]$ManifestPath)
{
    $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $preferred = @($manifest.surfaces | Where-Object { [string]$_.id -eq "the-alters-crafting-ugui" })
    if ($preferred.Count -gt 0)
    {
        return [string]$preferred[0].id
    }

    $firstUgui = @($manifest.surfaces | Where-Object { ([string]$_.id) -like "*-ugui" })
    if ($firstUgui.Count -gt 0)
    {
        return [string]$firstUgui[0].id
    }

    return [string]$manifest.surfaces[0].id
}

function Invoke-FrontierCandidateRun([object]$Candidate)
{
    $runRoot = Join-Path $OutputRoot $Candidate.CandidateId
    New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

    Write-Section "Generating fixtures for $($Candidate.Label)"
    Invoke-GenerateFixtureSet -RulesPath $Candidate.RulesPath -UGuiBuildProgramOverrides $Candidate.UGuiBuildProgramOverrides -EmitUGuiBuildProgramArtifacts $Candidate.EmitUGuiBuildProgramArtifacts

    Write-Section "Capturing fixtures for $($Candidate.Label)"
    $runManifestPath = New-RunCompareManifest -BaseManifestPath $CompareManifestPath -RunRoot $runRoot -Label $Candidate.CandidateId
    Invoke-UnityCapture -ManifestPath $runManifestPath

    Write-Section "Scoring fixtures for $($Candidate.Label)"
    $summary = Invoke-ScoreRun `
        -ManifestPath $runManifestPath `
        -RunRoot $runRoot `
        -Label $Candidate.Label `
        -RulePath $Candidate.RulesPath `
        -PlannedRulePath $Candidate.RulesPath `
        -PlanSummaryPath $null `
        -SelectedActions $Candidate.AppliedActionIds

    $Candidate | Add-Member -NotePropertyName RunSummaryPath -NotePropertyValue (Join-Path $runRoot "summary.json") -Force
    return $summary
}

function Invoke-StrictSweep([System.IO.FileInfo[]]$RuleFiles)
{
    $candidates = @()
    if (-not $SkipBaseline)
    {
        $candidates += New-SweepCandidate -Label "baseline-no-rules" -RulePath ""
    }

    foreach ($ruleFile in $RuleFiles)
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

    return $runSummaries
}

function Invoke-FrontierSweep([System.IO.FileInfo[]]$RuleFiles)
{
    $primarySurfaceId = Get-PrimarySurfaceId -ManifestPath $CompareManifestPath
    $staticActionLibrary = @(New-FrontierActionLibrary -RuleFiles $RuleFiles)
    $actionLibrary = @($staticActionLibrary)
    $actionMap = @{}

    $allCandidates = @()
    $candidateByKey = @{}
    $frontierIds = @()

    try
    {
        $baselineCandidate = [pscustomobject]@{
            CandidateId = "baseline"
            Label = "baseline-no-rules"
            Depth = 0
            ParentCandidateId = $null
            AppliedActionIds = @()
            RulesPath = ""
            UGuiBuildProgramOverrides = @{}
            EmitUGuiBuildProgramArtifacts = $true
            RunSummary = $null
        }
        $baselineCandidate.RunSummary = Invoke-FrontierCandidateRun -Candidate $baselineCandidate
        $allCandidates += $baselineCandidate
        $candidateByKey[""] = $baselineCandidate
        $frontierIds = @($baselineCandidate.CandidateId)

        $subtreeActionLibrary = @(New-UGuiSubtreeActionLibrary -LibraryRoot (Join-Path $OutputRoot "_subtree-library"))
        $actionLibrary = @($staticActionLibrary + $subtreeActionLibrary)
        foreach ($action in $actionLibrary)
        {
            $actionMap[$action.Id] = $action
        }

        for ($depth = 1; $depth -le $SearchDepth; $depth++)
        {
            $stageActions = @($actionLibrary | Where-Object { $_.Stage -eq $depth })
            if ($stageActions.Count -eq 0)
            {
                continue
            }

            $newCandidates = @()
            foreach ($frontierId in @($frontierIds))
            {
                $parent = @($allCandidates | Where-Object { $_.CandidateId -eq $frontierId })[0]
                if ($null -eq $parent)
                {
                    continue
                }

                $expandable = @(
                    $stageActions |
                        Where-Object { $parent.AppliedActionIds -notcontains $_.Id } |
                        Select-Object -First $ExpansionBudget
                )

                foreach ($action in $expandable)
                {
                    $actionIds = @($parent.AppliedActionIds + $action.Id)
                    $candidateKey = ($actionIds -join "|")
                    if ($candidateByKey.ContainsKey($candidateKey))
                    {
                        $newCandidates += $candidateByKey[$candidateKey]
                        continue
                    }

                    $candidateId = "frontier-d$depth-" + ([System.Math]::Abs($candidateKey.GetHashCode()))
                    $rulesPath = Join-Path (Join-Path $OutputRoot "_frontier-rules") "$candidateId.rules.json"
                    Write-FrontierRuleSet -OutputPath $rulesPath -ActionMap $actionMap -ActionIds $actionIds
                    $candidate = [pscustomobject]@{
                        CandidateId = $candidateId
                        Label = $candidateId
                        Depth = $depth
                        ParentCandidateId = $parent.CandidateId
                        AppliedActionIds = $actionIds
                        RulesPath = $rulesPath
                        UGuiBuildProgramOverrides = (New-CandidateUGuiBuildProgramOverrides -CandidateId $candidateId -ActionMap $actionMap -ActionIds $actionIds)
                        EmitUGuiBuildProgramArtifacts = $false
                        RunSummary = $null
                    }
                    $candidate.RunSummary = Invoke-FrontierCandidateRun -Candidate $candidate
                    $candidateByKey[$candidateKey] = $candidate
                    $allCandidates += $candidate
                    $newCandidates += $candidate
                }
            }

            if ($newCandidates.Count -eq 0)
            {
                continue
            }

            $optimizerRoot = Join-Path $OutputRoot "_optimizer"
            New-Item -ItemType Directory -Force -Path $optimizerRoot | Out-Null
            $statePath = Join-Path $optimizerRoot "depth-$depth.state.json"
            $summaryPath = Join-Path $optimizerRoot "depth-$depth.optimizer-summary.json"
            Write-FrontierOptimizerState -Path $statePath -PrimarySurfaceId $primarySurfaceId -BaselineCandidateId "baseline" -Candidates $allCandidates
            $optimizerSummary = Invoke-FrontierOptimizer -StatePath $statePath -SummaryPath $summaryPath
            $depthSummary = @($optimizerSummary.depths | Where-Object { [int]$_.depth -eq $depth })
            if ($depthSummary.Count -eq 0)
            {
                $frontierIds = @()
                continue
            }

            $frontierIds = @($depthSummary[0].retainedCandidateIds)
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

    $optimizerRoot = Join-Path $OutputRoot "_optimizer"
    $finalStatePath = Join-Path $optimizerRoot "final.state.json"
    $finalSummaryPath = Join-Path $OutputRoot "optimizer-summary.json"
    Write-FrontierOptimizerState -Path $finalStatePath -PrimarySurfaceId $primarySurfaceId -BaselineCandidateId "baseline" -Candidates $allCandidates
    $finalOptimizerSummary = Invoke-FrontierOptimizer -StatePath $finalStatePath -SummaryPath $finalSummaryPath

    foreach ($candidate in $allCandidates)
    {
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerCandidateId -NotePropertyValue $candidate.CandidateId -Force
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerDepth -NotePropertyValue $candidate.Depth -Force
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerActions -NotePropertyValue @($candidate.AppliedActionIds) -Force
    }

    $selectedCandidateId = [string]$finalOptimizerSummary.selectedCandidateId
    foreach ($candidate in $allCandidates)
    {
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerSelected -NotePropertyValue ($candidate.CandidateId -eq $selectedCandidateId) -Force
    }

    return @($allCandidates | ForEach-Object { $_.RunSummary })
}

function Invoke-CemSweep([System.IO.FileInfo[]]$RuleFiles)
{
    $primarySurfaceId = Get-PrimarySurfaceId -ManifestPath $CompareManifestPath
    $primaryDocumentKey = Get-PrimaryDocumentKey -PrimarySurfaceId $primarySurfaceId
    $staticActionLibrary = @(New-FrontierActionLibrary -RuleFiles $RuleFiles)
    $actionLibrary = @($staticActionLibrary)
    $actionMap = @{}
    $distributions = @()
    $random = if ($null -ne $RandomSeed) { [System.Random]::new([int]$RandomSeed) } else { [System.Random]::new() }

    $allCandidates = @()
    $candidateByKey = @{}

    try
    {
        $baselineCandidate = [pscustomobject]@{
            CandidateId = "baseline"
            Label = "baseline-no-rules"
            Depth = 0
            ParentCandidateId = $null
            AppliedActionIds = @()
            RulesPath = ""
            UGuiBuildProgramOverrides = @{}
            EmitUGuiBuildProgramArtifacts = $true
            RunSummary = $null
        }
        $baselineCandidate.RunSummary = Invoke-FrontierCandidateRun -Candidate $baselineCandidate
        $allCandidates += $baselineCandidate
        $candidateByKey[""] = $baselineCandidate

        $subtreeActionLibrary = @(New-UGuiSubtreeActionLibrary -LibraryRoot (Join-Path $OutputRoot "_subtree-library"))
        $actionLibrary = @($staticActionLibrary + $subtreeActionLibrary)
        foreach ($action in $actionLibrary)
        {
            $actionMap[$action.Id] = $action
        }

        $cemGroups = @(
            New-CemActionGroups -ActionLibrary $actionLibrary |
                Where-Object { Test-CemGroupMatchesFocus -Group $_ -Focus $CemFocus -PrimaryDocumentKey $primaryDocumentKey }
        )
        if ($cemGroups.Count -eq 0)
        {
            throw "No CEM action groups matched focus '$CemFocus'."
        }
        $distributions = @(New-CemDistributions -Groups $cemGroups -PrimaryDocumentKey $primaryDocumentKey)
        $warmStartPath = Resolve-AbsolutePath $CemWarmStartSummaryPath
        if (-not [string]::IsNullOrWhiteSpace($warmStartPath))
        {
            $warmStartCandidates = @(
                Get-CemWarmStartCandidates -SummaryPath $warmStartPath -MaxCount $CemEliteCount
            )
            if ($warmStartCandidates.Count -gt 0)
            {
                Update-CemDistributions -Distributions $distributions -EliteCandidates $warmStartCandidates -Smoothing 0.75
            }
        }

        $optimizerRoot = Join-Path $OutputRoot "_optimizer"
        New-Item -ItemType Directory -Force -Path $optimizerRoot | Out-Null

        for ($iteration = 1; $iteration -le $CemIterations; $iteration++)
        {
            $iterationCandidates = @()
            for ($sampleIndex = 1; $sampleIndex -le $CemSampleCount; $sampleIndex++)
            {
                $actionIds = @(Get-CemSampledActionIds -Distributions $distributions -Random $random -MaxActions $MaxActionsPerSample)
                $candidateKey = ($actionIds -join "|")
                if ($candidateByKey.ContainsKey($candidateKey))
                {
                    $iterationCandidates += $candidateByKey[$candidateKey]
                    continue
                }

                $candidateId = "cem-i$iteration-s$sampleIndex-" + ([System.Math]::Abs($candidateKey.GetHashCode()))
                $rulesPath = Join-Path (Join-Path $OutputRoot "_frontier-rules") "$candidateId.rules.json"
                Write-FrontierRuleSet -OutputPath $rulesPath -ActionMap $actionMap -ActionIds $actionIds

                $candidate = [pscustomobject]@{
                    CandidateId = $candidateId
                    Label = $candidateId
                    Depth = $iteration
                    ParentCandidateId = "baseline"
                    AppliedActionIds = @($actionIds)
                    RulesPath = $rulesPath
                    UGuiBuildProgramOverrides = (New-CandidateUGuiBuildProgramOverrides -CandidateId $candidateId -ActionMap $actionMap -ActionIds $actionIds)
                    EmitUGuiBuildProgramArtifacts = $false
                    RunSummary = $null
                }
                $candidate.RunSummary = Invoke-FrontierCandidateRun -Candidate $candidate
                $candidateByKey[$candidateKey] = $candidate
                $allCandidates += $candidate
                $iterationCandidates += $candidate
            }

            if ($iterationCandidates.Count -eq 0)
            {
                continue
            }

            $iterationCandidates = @(
                $iterationCandidates |
                    Where-Object { $_.CandidateId -ne "baseline" } |
                    Group-Object CandidateId |
                    ForEach-Object { $_.Group[0] }
            )

            if ($iterationCandidates.Count -eq 0)
            {
                continue
            }

            $statePath = Join-Path $optimizerRoot "cem-iteration-$iteration.state.json"
            $summaryPath = Join-Path $optimizerRoot "cem-iteration-$iteration.optimizer-summary.json"
            Write-FrontierOptimizerState -Path $statePath -PrimarySurfaceId $primarySurfaceId -BaselineCandidateId "baseline" -Candidates (@($baselineCandidate) + @($iterationCandidates))
            $optimizerSummary = Invoke-FrontierOptimizer -StatePath $statePath -SummaryPath $summaryPath

            $iterationIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($candidate in @($iterationCandidates))
            {
                $null = $iterationIds.Add([string]$candidate.CandidateId)
            }

            $eligibleElites = @(
                @($optimizerSummary.candidates) |
                    Where-Object { $iterationIds.Contains([string]$_.candidateId) -and [bool]$_.guardResult.passed } |
                    Sort-Object @{ Expression = "rank"; Ascending = $true }, @{ Expression = "candidateId"; Ascending = $true }
            )

            if ($eligibleElites.Count -eq 0)
            {
                $eligibleElites = @(
                    @($optimizerSummary.candidates) |
                        Where-Object { $iterationIds.Contains([string]$_.candidateId) } |
                        Sort-Object @{ Expression = "rank"; Ascending = $true }, @{ Expression = "candidateId"; Ascending = $true }
                )
            }

            $eliteEvaluations = @($eligibleElites | Select-Object -First ([Math]::Max(1, $CemEliteCount)))
            $eliteCandidates = @(
                $eliteEvaluations |
                    ForEach-Object {
                        $evaluation = $_
                        $match = @($iterationCandidates | Where-Object { $_.CandidateId -eq [string]$evaluation.candidateId })
                        if ($match.Count -gt 0)
                        {
                            $match[0]
                        }
                    } |
                    Where-Object { $null -ne $_ }
            )

            Update-CemDistributions -Distributions $distributions -EliteCandidates $eliteCandidates
            $snapshotPath = Join-Path $optimizerRoot "cem-iteration-$iteration.distribution.json"
            Write-CemIterationSnapshot -Path $snapshotPath -Iteration $iteration -Distributions $distributions -EliteEvaluations $eliteEvaluations
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

    $optimizerRoot = Join-Path $OutputRoot "_optimizer"
    $finalStatePath = Join-Path $optimizerRoot "final.state.json"
    $finalSummaryPath = Join-Path $OutputRoot "optimizer-summary.json"
    Write-FrontierOptimizerState -Path $finalStatePath -PrimarySurfaceId $primarySurfaceId -BaselineCandidateId "baseline" -Candidates $allCandidates
    $finalOptimizerSummary = Invoke-FrontierOptimizer -StatePath $finalStatePath -SummaryPath $finalSummaryPath

    foreach ($candidate in $allCandidates)
    {
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerCandidateId -NotePropertyValue $candidate.CandidateId -Force
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerDepth -NotePropertyValue $candidate.Depth -Force
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerActions -NotePropertyValue @($candidate.AppliedActionIds) -Force
    }

    $selectedCandidateId = [string]$finalOptimizerSummary.selectedCandidateId
    foreach ($candidate in $allCandidates)
    {
        $candidate.RunSummary | Add-Member -NotePropertyName optimizerSelected -NotePropertyValue ($candidate.CandidateId -eq $selectedCandidateId) -Force
    }

    return @($allCandidates | ForEach-Object { $_.RunSummary })
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

$runSummaries = switch ($OptimizerMode)
{
    "frontier" { Invoke-FrontierSweep -RuleFiles $ruleFiles; break }
    "cem" { Invoke-CemSweep -RuleFiles $ruleFiles; break }
    default { Invoke-StrictSweep -RuleFiles $ruleFiles; break }
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
                motion = Get-OptionalPropertyValue -Source $_ -PropertyName "motion"
            }
        }
)

$leaderboard = [pscustomobject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    compareManifestPath = $CompareManifestPath
    normalization = $Normalization
    tolerance = $Tolerance
    optimizerMode = $OptimizerMode
    beamWidth = $BeamWidth
    searchDepth = $SearchDepth
    expansionBudget = $ExpansionBudget
    baselineLabel = if ($null -ne $baselineSummary) { $baselineSummary.label } else { $null }
    entries = $rankedSummaries
}

$leaderboardPath = Join-Path $OutputRoot "leaderboard.json"
$leaderboard | ConvertTo-Json -Depth 20 | Set-Content -Path $leaderboardPath

Write-Host ""
Write-Host "Fixture rule sweep complete."
Write-Host "Leaderboard: $leaderboardPath"
