<#
.SYNOPSIS
    Verifies BoomHud package IDs and package dependency graph.

.DESCRIPTION
    Packs the split-ready BoomHud projects and inspects their generated nuspec files.
    This enforces the first separation slice for Foundation, input parsers, and active backends.

.EXAMPLE
    pwsh -NoProfile -File scripts/verify-package-graph.ps1

.NOTES
    Exit codes:
      0 - Package graph matches expectations
      1 - Package graph mismatch or pack failure
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$packRoot = Join-Path $repoRoot 'build' '_artifacts' 'boom-hud' 'package-graph-check'

if (Test-Path $packRoot) {
    Remove-Item $packRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $packRoot | Out-Null

$projects = @(
    @{
        Name = 'Foundation'
        Project = 'dotnet/src/BoomHud.Abstractions/BoomHud.Abstractions.csproj'
        PackageId = 'BoomHud.Foundation'
        Dependencies = @()
    },
    @{
        Name = 'FoundationGenerators'
        Project = 'dotnet/src/BoomHud.Generators/BoomHud.Generators.csproj'
        PackageId = 'BoomHud.Foundation.Generators'
        Dependencies = @('BoomHud.Foundation')
    },
    @{
        Name = 'InputFigma'
        Project = 'dotnet/src/BoomHud.Dsl/BoomHud.Dsl.csproj'
        PackageId = 'BoomHud.Input.Figma'
        Dependencies = @('BoomHud.Foundation')
    },
    @{
        Name = 'InputPencil'
        Project = 'dotnet/src/BoomHud.Dsl.Pencil/BoomHud.Dsl.Pencil.csproj'
        PackageId = 'BoomHud.Input.Pencil'
        Dependencies = @('BoomHud.Foundation')
    },
    @{
        Name = 'Godot'
        Project = 'dotnet/src/BoomHud.Gen.Godot/BoomHud.Gen.Godot.csproj'
        PackageId = 'BoomHud.Godot'
        Dependencies = @('BoomHud.Foundation', 'BoomHud.Foundation.Generators')
    },
    @{
        Name = 'TerminalGui'
        Project = 'dotnet/src/BoomHud.Gen.TerminalGui/BoomHud.Gen.TerminalGui.csproj'
        PackageId = 'BoomHud.TerminalGui'
        Dependencies = @('BoomHud.Foundation', 'BoomHud.Foundation.Generators')
    },
    @{
        Name = 'Unity'
        Project = 'dotnet/src/BoomHud.Gen.Unity/BoomHud.Gen.Unity.csproj'
        PackageId = 'BoomHud.Unity'
        Dependencies = @('BoomHud.Foundation', 'BoomHud.Foundation.Generators')
    }
)

function Get-NuspecMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        if (-not $entry) {
            throw "No nuspec found in package: $PackagePath"
        }

        $reader = New-Object System.IO.StreamReader($entry.Open())
        try {
            $xml = [xml]$reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $zip.Dispose()
    }

    $namespace = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $namespace.AddNamespace('ns', $xml.DocumentElement.NamespaceURI)

    $packageId = $xml.SelectSingleNode('/ns:package/ns:metadata/ns:id', $namespace).InnerText
    $dependencyNodes = $xml.SelectNodes('/ns:package/ns:metadata/ns:dependencies/ns:group/ns:dependency', $namespace)
    $dependencyIds = @($dependencyNodes | ForEach-Object { $_.id })

    return @{
        PackageId = $packageId
        DependencyIds = $dependencyIds
    }
}

Write-Host 'Packing split-ready BoomHud packages...' -ForegroundColor Cyan

foreach ($project in $projects) {
    $projectPath = Join-Path $repoRoot $project.Project
    $outputDir = Join-Path $packRoot $project.Name

    Write-Host "  Packing $($project.PackageId)..." -ForegroundColor DarkCyan
    dotnet pack $projectPath -o $outputDir | Out-Null

    $package = Get-ChildItem -Path $outputDir -Filter '*.nupkg' | Select-Object -First 1
    if (-not $package) {
        throw "No nupkg produced for $($project.PackageId)"
    }

    $metadata = Get-NuspecMetadata -PackagePath $package.FullName

    if ($metadata.PackageId -ne $project.PackageId) {
        throw "Package ID mismatch for $($project.Project). Expected '$($project.PackageId)', got '$($metadata.PackageId)'."
    }

    $expectedDependencies = @($project.Dependencies | Sort-Object)
    $actualDependencies = @($metadata.DependencyIds | Sort-Object)

    $dependenciesMatch = $expectedDependencies.Count -eq $actualDependencies.Count
    if ($dependenciesMatch -and $expectedDependencies.Count -gt 0) {
        $dependenciesMatch = (($expectedDependencies -join '|') -ceq ($actualDependencies -join '|'))
    }

    if (-not $dependenciesMatch) {
        $expected = if ($expectedDependencies.Count -eq 0) { '<none>' } else { $expectedDependencies -join ', ' }
        $actual = if ($actualDependencies.Count -eq 0) { '<none>' } else { $actualDependencies -join ', ' }
        throw "Dependency mismatch for $($project.PackageId). Expected: $expected. Actual: $actual."
    }
}

Write-Host 'OK: Package IDs and dependency graph match the current separation contract.' -ForegroundColor Green
exit 0