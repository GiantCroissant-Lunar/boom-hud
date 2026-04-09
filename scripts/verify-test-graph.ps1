<#
.SYNOPSIS
    Verifies BoomHud test-project ownership and dependency boundaries.

.DESCRIPTION
    Enforces the current split between Foundation-owned tests and backend/tooling tests.
    Checks the test project references, compile include/remove rules, and solution registration.

.EXAMPLE
    pwsh -NoProfile -File scripts/verify-test-graph.ps1

.NOTES
    Exit codes:
      0 - Test graph matches expectations
      1 - Test graph mismatch
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$unitProjectPath = Join-Path $repoRoot 'dotnet\tests\BoomHud.Tests.Unit\BoomHud.Tests.Unit.csproj'
$backendProjectPath = Join-Path $repoRoot 'dotnet\tests\BoomHud.Tests.Backends\BoomHud.Tests.Backends.csproj'
$solutionPath = Join-Path $repoRoot 'dotnet\BoomHud.sln'

function Get-ProjectItems {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,
        [Parameter(Mandatory = $true)]
        [string] $ElementName,
        [Parameter(Mandatory = $true)]
        [string] $AttributeName
    )

    [xml]$xml = Get-Content -Path $ProjectPath -Raw
    return @(
        $xml.Project.ItemGroup.$ElementName |
            Where-Object { $_ -and $_.$AttributeName } |
            ForEach-Object { [string]$_.($AttributeName) }
    )
}

function Assert-SetEquals {
    param(
        [Parameter(Mandatory = $true)] [string] $Label,
        [Parameter(Mandatory = $true)] [string[]] $Expected,
        [Parameter(Mandatory = $true)] [string[]] $Actual
    )

    $expectedSorted = @($Expected | Sort-Object)
    $actualSorted = @($Actual | Sort-Object)

    $match = $expectedSorted.Count -eq $actualSorted.Count
    if ($match -and $expectedSorted.Count -gt 0) {
        $match = (($expectedSorted -join '|') -ceq ($actualSorted -join '|'))
    }

    if (-not $match) {
        $expectedText = if ($expectedSorted.Count -eq 0) { '<none>' } else { $expectedSorted -join ', ' }
        $actualText = if ($actualSorted.Count -eq 0) { '<none>' } else { $actualSorted -join ', ' }
        throw "$Label mismatch. Expected: $expectedText. Actual: $actualText."
    }
}

Write-Host 'Checking Foundation/backend test graph...' -ForegroundColor Cyan

$unitExpectedRefs = @(
    '..\..\src\BoomHud.Abstractions\BoomHud.Abstractions.csproj',
    '..\..\src\BoomHud.Dsl\BoomHud.Dsl.csproj',
    '..\..\src\BoomHud.Dsl.Pencil\BoomHud.Dsl.Pencil.csproj',
    '..\..\src\BoomHud.Mvvm.Generators\BoomHud.Mvvm.Generators.csproj'
)

$backendExpectedRefs = @(
    '..\..\src\BoomHud.Abstractions\BoomHud.Abstractions.csproj',
    '..\..\src\BoomHud.Cli\BoomHud.Cli.csproj',
    '..\..\src\BoomHud.Dsl\BoomHud.Dsl.csproj',
    '..\..\src\BoomHud.Dsl.Pencil\BoomHud.Dsl.Pencil.csproj',
    '..\..\src\BoomHud.Generators\BoomHud.Generators.csproj',
    '..\..\src\BoomHud.Gen.TerminalGui\BoomHud.Gen.TerminalGui.csproj',
    '..\..\src\BoomHud.Gen.Avalonia\BoomHud.Gen.Avalonia.csproj',
    '..\..\src\BoomHud.Gen.Godot\BoomHud.Gen.Godot.csproj',
    '..\..\src\BoomHud.Gen.Unity\BoomHud.Gen.Unity.csproj'
)

$unitExpectedRemoves = @(
    'Capabilities\**\*.cs',
    'Cli\**\*.cs',
    'Generation\**\*.cs',
    'Integration\**\*.cs',
    'Snapshots\BaselineCompareHandlerTests.cs',
    'Snapshots\BaselineDiffHandlerTests.cs'
)

$backendExpectedIncludes = @(
    '..\BoomHud.Tests.Unit\Capabilities\**\*.cs',
    '..\BoomHud.Tests.Unit\Cli\**\*.cs',
    '..\BoomHud.Tests.Unit\Generation\**\*.cs',
    '..\BoomHud.Tests.Unit\Integration\**\*.cs',
    '..\BoomHud.Tests.Unit\Snapshots\BaselineCompareHandlerTests.cs',
    '..\BoomHud.Tests.Unit\Snapshots\BaselineDiffHandlerTests.cs'
)

$unitRefs = Get-ProjectItems -ProjectPath $unitProjectPath -ElementName 'ProjectReference' -AttributeName 'Include'
$backendRefs = Get-ProjectItems -ProjectPath $backendProjectPath -ElementName 'ProjectReference' -AttributeName 'Include'
$unitRemoves = Get-ProjectItems -ProjectPath $unitProjectPath -ElementName 'Compile' -AttributeName 'Remove'
$backendIncludes = Get-ProjectItems -ProjectPath $backendProjectPath -ElementName 'Compile' -AttributeName 'Include'

Assert-SetEquals -Label 'Foundation test project references' -Expected $unitExpectedRefs -Actual $unitRefs
Assert-SetEquals -Label 'Backend test project references' -Expected $backendExpectedRefs -Actual $backendRefs
Assert-SetEquals -Label 'Foundation test project compile removals' -Expected $unitExpectedRemoves -Actual $unitRemoves
Assert-SetEquals -Label 'Backend test project compile includes' -Expected $backendExpectedIncludes -Actual $backendIncludes

$solutionText = Get-Content -Path $solutionPath -Raw
if ($solutionText -notmatch 'tests\\BoomHud\.Tests\.Backends\\BoomHud\.Tests\.Backends\.csproj') {
    throw 'BoomHud.Tests.Backends is not registered in dotnet/BoomHud.sln.'
}

Write-Host 'OK: Foundation/backend test graph matches the current separation contract.' -ForegroundColor Green
exit 0