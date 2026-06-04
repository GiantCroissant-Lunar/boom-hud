[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$PenPath = "samples/pencil/the-alters-crafting.pen",
    [string]$ReferenceImage = "build/source-refs/interfaceingame/the-alters-crafting/TAC-source.png",
    [string]$PromptTemplate = "scripts/prompts/kimi-pen-slot-autoresearch.md",
    [string]$OutputRoot = "build/kimi-pen-autoresearch",
    [string]$ScreenNodeId = "TAC01",
    [string]$TargetNodeId = "1OXeQ",
    [string]$ComponentNodeId = "SE0mQ",
    [double]$BaselineScore = 80.1591,
    [int]$CropX = 176,
    [int]$CropY = 187,
    [int]$CropWidth = 165,
    [int]$CropHeight = 240,
    [int]$AttemptCount = 1,
    [string]$Model = "kimi-code/kimi-for-coding",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Resolve-AbsolutePath([string]$BasePath, [string]$PathValue)
{
    if ([string]::IsNullOrWhiteSpace($PathValue))
    {
        return $PathValue
    }

    if ([System.IO.Path]::IsPathRooted($PathValue))
    {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

function Write-Utf8NoBomFile([string]$Path, [string]$Content)
{
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Get-PreferredShellExecutable()
{
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -ne $pwsh -and -not [string]::IsNullOrWhiteSpace($pwsh.Source))
    {
        return $pwsh.Source
    }

    $powershell = Get-Command powershell -ErrorAction SilentlyContinue
    if ($null -ne $powershell -and -not [string]::IsNullOrWhiteSpace($powershell.Source))
    {
        return $powershell.Source
    }

    throw "Could not find pwsh or powershell."
}

function New-IsolatedKimiConfig([string]$Directory, [string]$ModelName)
{
    $configPath = Join-Path $Directory "kimi-config.toml"
    $content = @"
default_model = "$ModelName"
default_thinking = true
default_yolo = true

[models."$ModelName"]
provider = "managed:kimi-code"
model = "kimi-for-coding"
max_context_size = 262144
capabilities = ["thinking", "image_in", "video_in"]

[providers."managed:kimi-code"]
type = "kimi"
base_url = "https://api.kimi.com/coding/v1"
api_key = ""

[providers."managed:kimi-code".oauth]
storage = "file"
key = "oauth/kimi-code"

[loop_control]
max_steps_per_turn = 12
max_retries_per_step = 1
max_ralph_iterations = 0
reserved_context_size = 50000
"@

    Write-Utf8NoBomFile -Path $configPath -Content $content
    return $configPath
}

function New-PencilOnlyMcpConfig([string]$Directory)
{
    $mcpPath = Join-Path $Directory "kimi-mcp.json"
    $content = @'
{
  "mcpServers": {
    "pencil": {
      "command": "C:/Users/User/AppData/Local/Programs/Pencil/resources/app.asar.unpacked/out/mcp-server-windows-x64.exe",
      "args": ["--app", "desktop"]
    }
  }
}
'@

    Write-Utf8NoBomFile -Path $mcpPath -Content $content
    return $mcpPath
}

if ([string]::IsNullOrWhiteSpace($RepoRoot))
{
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSCommandPath) ".."))
}
else
{
    $RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
}

$penAbsolutePath = Resolve-AbsolutePath -BasePath $RepoRoot -PathValue $PenPath
$referenceAbsolutePath = Resolve-AbsolutePath -BasePath $RepoRoot -PathValue $ReferenceImage
$promptTemplateAbsolutePath = Resolve-AbsolutePath -BasePath $RepoRoot -PathValue $PromptTemplate
$outputRootAbsolutePath = Resolve-AbsolutePath -BasePath $RepoRoot -PathValue $OutputRoot
$measureScriptPath = Join-Path $RepoRoot "scripts/measure-image-crop-to-crop.ps1"
$shellExecutable = Get-PreferredShellExecutable

$attemptId = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Join-Path $outputRootAbsolutePath $attemptId
$scoreOutputDir = Join-Path $outputDir "score"
$backupPenPath = Join-Path $outputDir ([System.IO.Path]::GetFileNameWithoutExtension($penAbsolutePath) + ".backup.pen")
$exportedScreenPath = Join-Path $outputDir "TAC01.png"
$scoreSummaryPath = Join-Path $scoreOutputDir "image-summary.json"
$promptPath = Join-Path $outputDir "kimi-prompt.md"
$resultPath = Join-Path $outputDir "kimi-final-message.txt"

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $scoreOutputDir -Force | Out-Null

$template = Get-Content -Path $promptTemplateAbsolutePath -Raw
$replacements = @{
    "{{RepoRoot}}" = $RepoRoot
    "{{PenPath}}" = $penAbsolutePath
    "{{ReferenceImage}}" = $referenceAbsolutePath
    "{{ScreenNodeId}}" = $ScreenNodeId
    "{{TargetNodeId}}" = $TargetNodeId
    "{{ComponentNodeId}}" = $ComponentNodeId
    "{{CropX}}" = [string]$CropX
    "{{CropY}}" = [string]$CropY
    "{{CropWidth}}" = [string]$CropWidth
    "{{CropHeight}}" = [string]$CropHeight
    "{{BaselineScore}}" = $BaselineScore.ToString("0.####", [System.Globalization.CultureInfo]::InvariantCulture)
    "{{OutputDir}}" = $outputDir
    "{{BackupPenPath}}" = $backupPenPath
    "{{ExportedScreenPath}}" = $exportedScreenPath
    "{{MeasureScriptPath}}" = $measureScriptPath
    "{{ShellExecutable}}" = $shellExecutable
    "{{ScoreOutputDir}}" = $scoreOutputDir
    "{{ScoreSummaryPath}}" = $scoreSummaryPath
    "{{AttemptCount}}" = [string]$AttemptCount
}

foreach ($key in $replacements.Keys)
{
    $template = $template.Replace($key, $replacements[$key])
}

Write-Utf8NoBomFile -Path $promptPath -Content $template

$kimiConfigPath = New-IsolatedKimiConfig -Directory $outputDir -ModelName $Model
$kimiMcpPath = New-PencilOnlyMcpConfig -Directory $outputDir

$commandPreview = @(
    "kimi",
    "--config-file `"$kimiConfigPath`"",
    "--mcp-config-file `"$kimiMcpPath`"",
    "--work-dir `"$RepoRoot`"",
    "--model `"$Model`"",
    "--quiet",
    "-p @`"$promptPath`""
) -join " "

if ($DryRun)
{
    [pscustomobject]@{
        OutputDir = $outputDir
        PromptPath = $promptPath
        BackupPenPath = $backupPenPath
        ScoreOutputDir = $scoreOutputDir
        Command = $commandPreview
    } | ConvertTo-Json -Depth 4
    exit 0
}

$prompt = Get-Content -Path $promptPath -Raw
$finalMessage = & kimi `
    --config-file $kimiConfigPath `
    --mcp-config-file $kimiMcpPath `
    --work-dir $RepoRoot `
    --model $Model `
    --quiet `
    -p $prompt

if ($LASTEXITCODE -ne 0)
{
    throw "Kimi CLI failed with exit code $LASTEXITCODE."
}

Write-Utf8NoBomFile -Path $resultPath -Content $finalMessage
$finalMessage
