using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.Composition;
using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Abstractions.Tokens;
using BoomHud.Cli.Commands.Baseline;
using BoomHud.Cli.Handlers.Baseline;
using BoomHud.Dsl;
using BoomHud.Dsl.Figma;
using BoomHud.Dsl.Pencil;
using BoomHud.Gen.Avalonia;
using BoomHud.Gen.Godot;
using BoomHud.Gen.TerminalGui;
using Path = System.IO.Path;

namespace BoomHud.Cli;

/// <summary>
/// BoomHud CLI entry point.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var rootCommand = new RootCommand("BoomHud - UI code generator from Figma JSON");

            // Generate command
            var generateCommand = new Command("generate", "Generate UI code from design files (Figma JSON, Pencil .pen, or BoomHud IR)");
            var inputArg = new Argument<FileInfo?>("input", () => null, "Input design file (.pen, .figma.json, or IR JSON). Use --input for multiple files.");
            var inputOption = new Option<FileInfo[]>("--input", "Input design files (can be specified multiple times for composition)")
            {
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            inputOption.AddAlias("--in");
            var rootComponentOption = new Option<string?>("--root", "Root component name for multi-input composition. If omitted, first input's root is used.");
            var manifestOption = new Option<FileInfo?>("--manifest", "Compose manifest file (boom-hud.compose.json). Defines sources, root, tokens, and targets.");
            var targetOption = new Option<string>("--target", () => "terminalGui", "Target backend (terminalGui, avalonia, godot, all)");
            var formatOption = new Option<string?>("--format", "Input format (pen, figma, ir). Auto-detected from extension if omitted.");
            var outputOption = new Option<DirectoryInfo?>("--output", "Output directory for generated files");
            var namespaceOption = new Option<string>("--namespace", () => "Generated", "Namespace for generated code");
            var viewModelNamespaceOption = new Option<string?>("--viewmodel-namespace", "Namespace for generated (or externally-provided) ViewModel interfaces (defaults to --namespace)");
            var noVmInterfacesOption = new Option<bool>("--no-vm-interfaces", "Do not emit I*ViewModel.g.cs interfaces (assume provided externally)");
            var composeHelpersOption = new Option<bool>("--compose", "Emit *Compose.g.cs composition helpers");
            var tscnOption = new Option<bool>("--tscn", "Emit Godot scene files (*.tscn) in addition to C# (Godot target only)");
            var tscnNoScriptOption = new Option<bool>("--tscn-no-script", "Emit Godot scene files (*.tscn) without attaching generated C# script (Godot target only)");
            var contractIdOption = new Option<string?>("--contract-id", "Optional contract ID to embed into generated views for drift detection");
            var nodeOption = new Option<string?>("--node", "Optional Figma node id to use as the root (e.g. \"1:2\")");
            var annotationsOption = new Option<FileInfo?>("--annotations", "Optional BoomHud annotations JSON file (bindings, semantics) applied after parsing");
            var variablesOption = new Option<FileInfo?>("--variables", "Optional Figma variables JSON file for theme tokens");
            var themeNameOption = new Option<string?>("--theme-name", "Optional theme name (defaults to variables filename)");
            var themeCollectionOption = new Option<string?>("--theme-collection", "Optional Figma variables collection name");
            var themeModeOption = new Option<string?>("--theme-mode", "Optional Figma variables mode id or name (e.g. \"Light\")");
            var descriptionReplaceOption = new Option<string[]>("--description-replace", "Optional description replacements in the form 'from=to' (can be specified multiple times)")
            {
                Arity = ArgumentArity.ZeroOrMore
            };
            var tokensOption = new Option<FileInfo?>("--tokens", "Token registry file (tokens.ir.json). If omitted, looks for ui/tokens.ir.json relative to input file.");

            generateCommand.AddArgument(inputArg);
            generateCommand.AddOption(inputOption);
            generateCommand.AddOption(rootComponentOption);
            generateCommand.AddOption(manifestOption);
            generateCommand.AddOption(formatOption);
            generateCommand.AddOption(targetOption);
            generateCommand.AddOption(outputOption);
            generateCommand.AddOption(namespaceOption);
            generateCommand.AddOption(viewModelNamespaceOption);
            generateCommand.AddOption(noVmInterfacesOption);
            generateCommand.AddOption(composeHelpersOption);
            generateCommand.AddOption(tscnOption);
            generateCommand.AddOption(tscnNoScriptOption);
            generateCommand.AddOption(contractIdOption);
            generateCommand.AddOption(nodeOption);
            generateCommand.AddOption(annotationsOption);
            generateCommand.AddOption(variablesOption);
            generateCommand.AddOption(themeNameOption);
            generateCommand.AddOption(themeCollectionOption);
            generateCommand.AddOption(themeModeOption);
            generateCommand.AddOption(descriptionReplaceOption);
            generateCommand.AddOption(tokensOption);

            generateCommand.SetHandler((InvocationContext context) =>
            {
                var inputSingle = context.ParseResult.GetValueForArgument(inputArg);
                var inputMultiple = context.ParseResult.GetValueForOption(inputOption) ?? [];
                var rootComponent = context.ParseResult.GetValueForOption(rootComponentOption);
                var manifest = context.ParseResult.GetValueForOption(manifestOption);
                var format = context.ParseResult.GetValueForOption(formatOption);
                var target = context.ParseResult.GetValueForOption(targetOption) ?? "terminalGui";
                var output = context.ParseResult.GetValueForOption(outputOption);
                var @namespace = context.ParseResult.GetValueForOption(namespaceOption) ?? "Generated";
                var viewModelNamespace = context.ParseResult.GetValueForOption(viewModelNamespaceOption);
                var noVmInterfaces = context.ParseResult.GetValueForOption(noVmInterfacesOption);
                var composeHelpers = context.ParseResult.GetValueForOption(composeHelpersOption);
                var tscn = context.ParseResult.GetValueForOption(tscnOption);
                var tscnNoScript = context.ParseResult.GetValueForOption(tscnNoScriptOption);
                var contractId = context.ParseResult.GetValueForOption(contractIdOption);
                var node = context.ParseResult.GetValueForOption(nodeOption);
                var annotations = context.ParseResult.GetValueForOption(annotationsOption);
                var variables = context.ParseResult.GetValueForOption(variablesOption);
                var themeName = context.ParseResult.GetValueForOption(themeNameOption);
                var themeCollection = context.ParseResult.GetValueForOption(themeCollectionOption);
                var themeMode = context.ParseResult.GetValueForOption(themeModeOption);
                var descriptionReplacements = context.ParseResult.GetValueForOption(descriptionReplaceOption);
                var tokens = context.ParseResult.GetValueForOption(tokensOption);

                // Load manifest if provided
                ComposeManifest? loadedManifest = null;
                if (manifest != null)
                {
                    if (!manifest.Exists)
                    {
                        Console.Error.WriteLine($"Error: Manifest file not found: {manifest.FullName}");
                        context.ExitCode = 1;
                        return;
                    }

                    Console.WriteLine($"Loading compose manifest: {manifest.FullName}");
                    loadedManifest = ComposeManifest.LoadFromFile(manifest.FullName);
                }

                // Merge inputs: CLI args take precedence, then manifest
                var inputs = new List<FileInfo>();
                if (inputSingle != null)
                    inputs.Add(inputSingle);
                inputs.AddRange(inputMultiple);

                // If no CLI inputs, use manifest sources
                if (inputs.Count == 0 && loadedManifest != null)
                {
                    var manifestPaths = loadedManifest.ResolveSourcePaths(manifest!.FullName);
                    foreach (var path in manifestPaths)
                    {
                        inputs.Add(new FileInfo(path));
                    }
                }

                if (inputs.Count == 0)
                {
                    Console.Error.WriteLine("Error: No input files specified. Use positional argument, --input option, or --manifest.");
                    context.ExitCode = 1;
                    return;
                }

                // Merge root: CLI takes precedence
                var effectiveRoot = rootComponent ?? loadedManifest?.Root;

                // Merge tokens: CLI takes precedence
                var effectiveTokens = tokens;
                if (effectiveTokens == null && loadedManifest?.Tokens != null)
                {
                    var manifestTokensPath = loadedManifest.ResolveTokensPath(manifest!.FullName);
                    if (manifestTokensPath != null)
                    {
                        effectiveTokens = new FileInfo(manifestTokensPath);
                    }
                }

                // Merge output: CLI takes precedence
                var effectiveOutput = output;
                if (effectiveOutput == null && loadedManifest?.Output != null)
                {
                    var manifestOutputPath = loadedManifest.ResolveOutputPath(manifest!.FullName);
                    if (manifestOutputPath != null)
                    {
                        effectiveOutput = new DirectoryInfo(manifestOutputPath);
                    }
                }

                // Merge namespace: CLI takes precedence (but only if explicitly set)
                var effectiveNamespace = @namespace;
                if (effectiveNamespace == "Generated" && loadedManifest?.Namespace != null)
                {
                    effectiveNamespace = loadedManifest.Namespace;
                }

                // Merge target: CLI takes precedence (but only if explicitly set)
                var effectiveTarget = target;
                if (effectiveTarget == "terminalGui" && loadedManifest?.Targets?.Count > 0)
                {
                    effectiveTarget = string.Join(",", loadedManifest.Targets);
                }

                context.ExitCode = HandleGenerate(
                    inputs.ToArray(),
                    effectiveRoot,
                    format,
                    effectiveTarget,
                    effectiveOutput,
                    effectiveNamespace,
                    viewModelNamespace,
                    noVmInterfaces,
                    composeHelpers,
                    tscn || tscnNoScript,
                    tscnNoScript,
                    contractId,
                    node,
                    annotations,
                    variables,
                    themeName,
                    themeCollection,
                    themeMode,
                    descriptionReplacements,
                    effectiveTokens);
            });

            // Validate command
            var validateCommand = new Command("validate", "Validate Figma JSON input (and optional theme variables)");
            var validateInputArg = new Argument<FileInfo>("input", "Input Figma JSON file to validate");
            var validateNodeOption = new Option<string?>("--node", "Optional Figma node id to test parsing (e.g. \"1:2\")");
            var validateVariablesOption = new Option<FileInfo?>("--variables", "Optional Figma variables JSON file to validate");
            var validateThemeCollectionOption = new Option<string?>("--theme-collection", "Optional Figma variables collection name");
            var validateThemeModeOption = new Option<string?>("--theme-mode", "Optional Figma variables mode id or name (e.g. \"Light\")");

            validateCommand.AddArgument(validateInputArg);
            validateCommand.AddOption(validateNodeOption);
            validateCommand.AddOption(validateVariablesOption);
            validateCommand.AddOption(validateThemeCollectionOption);
            validateCommand.AddOption(validateThemeModeOption);
            validateCommand.SetHandler((InvocationContext context) =>
            {
                var input = context.ParseResult.GetValueForArgument(validateInputArg);
                var node = context.ParseResult.GetValueForOption(validateNodeOption);
                var variables = context.ParseResult.GetValueForOption(validateVariablesOption);
                var themeCollection = context.ParseResult.GetValueForOption(validateThemeCollectionOption);
                var themeMode = context.ParseResult.GetValueForOption(validateThemeModeOption);

                HandleValidate(input, node, variables, themeCollection, themeMode);
            });

            // Init command (scaffold new project)
            var initCommand = new Command("init", "Initialize a new BoomHud project");
            var projectNameArg = new Argument<string>("name", "Project name");

            initCommand.AddArgument(projectNameArg);
            initCommand.SetHandler(HandleInit, projectNameArg);

            var uiproCommand = new Command("uipro", "Run UI/UX Pro Max search/design-system tooling (python)");
            var uiproQueryArg = new Argument<string>("query", "Search query (keywords)");
            var uiproPythonOption = new Option<string>("--python", () => "python", "Python executable to use (e.g., python, python3, py)");
            var uiproScriptOption = new Option<string?>(
                "--script",
                "Optional path to ui-ux-pro-max search.py (defaults to the vendored skill tooling if present)");

            var uiproDomainOption = new Option<string?>(new[] { "--domain", "-d" }, "Domain search (style, color, typography, ux, landing, product, chart, icons, web, react)");
            var uiproStackOption = new Option<string?>(new[] { "--stack", "-s" }, "Stack-specific search (html-tailwind, react, nextjs, ...)");
            var uiproMaxResultsOption = new Option<int>(new[] { "--max-results", "-n" }, () => 3, "Max results");
            var uiproJsonOption = new Option<bool>("--json", "Output JSON (for domain/stack search)");

            var uiproDesignSystemOption = new Option<bool>(new[] { "--design-system", "-ds" }, "Generate complete design system recommendation");
            var uiproProjectNameOption = new Option<string?>(new[] { "--project-name", "-p" }, "Project name for design system output");
            var uiproFormatOption = new Option<string>(new[] { "--format", "-f" }, () => "ascii", "Design system output format (ascii, markdown)");
            var uiproPersistOption = new Option<bool>("--persist", "Persist design system to design-system/<project>/MASTER.md");
            var uiproPageOption = new Option<string?>("--page", "Page name for override file (design-system/<project>/pages/<page>.md)");
            var uiproOutputDirOption = new Option<DirectoryInfo?>(new[] { "--output-dir", "-o" }, "Output directory for persisted files (defaults to current directory)");

            uiproCommand.AddArgument(uiproQueryArg);
            uiproCommand.AddOption(uiproPythonOption);
            uiproCommand.AddOption(uiproScriptOption);
            uiproCommand.AddOption(uiproDomainOption);
            uiproCommand.AddOption(uiproStackOption);
            uiproCommand.AddOption(uiproMaxResultsOption);
            uiproCommand.AddOption(uiproJsonOption);
            uiproCommand.AddOption(uiproDesignSystemOption);
            uiproCommand.AddOption(uiproProjectNameOption);
            uiproCommand.AddOption(uiproFormatOption);
            uiproCommand.AddOption(uiproPersistOption);
            uiproCommand.AddOption(uiproPageOption);
            uiproCommand.AddOption(uiproOutputDirOption);

            uiproCommand.SetHandler((InvocationContext context) =>
            {
                var query = context.ParseResult.GetValueForArgument(uiproQueryArg);
                var pythonExe = context.ParseResult.GetValueForOption(uiproPythonOption) ?? "python";
                var script = context.ParseResult.GetValueForOption(uiproScriptOption);
                var domain = context.ParseResult.GetValueForOption(uiproDomainOption);
                var stack = context.ParseResult.GetValueForOption(uiproStackOption);
                var maxResults = context.ParseResult.GetValueForOption(uiproMaxResultsOption);
                var jsonOut = context.ParseResult.GetValueForOption(uiproJsonOption);
                var designSystem = context.ParseResult.GetValueForOption(uiproDesignSystemOption);
                var projectName = context.ParseResult.GetValueForOption(uiproProjectNameOption);
                var format = context.ParseResult.GetValueForOption(uiproFormatOption) ?? "ascii";
                var persist = context.ParseResult.GetValueForOption(uiproPersistOption);
                var page = context.ParseResult.GetValueForOption(uiproPageOption);
                var outputDir = context.ParseResult.GetValueForOption(uiproOutputDirOption);

                context.ExitCode = HandleUiProMax(
                    query,
                    pythonExe,
                    script,
                    domain,
                    stack,
                    maxResults,
                    jsonOut,
                    designSystem,
                    projectName,
                    format,
                    persist,
                    page,
                    outputDir);
            });

            // Snapshot command
            var snapshotCommand = new Command("snapshot", "Generate deterministic PNG snapshots from rendered UI states");
            var snapshotManifestOption = new Option<FileInfo?>("--manifest", "Compose manifest file (boom-hud.compose.json)");
            var snapshotStatesOption = new Option<FileInfo?>("--states", "States manifest file (*.states.json) defining viewport and VM states to render");
            var snapshotTargetOption = new Option<string>("--target", () => "godot", "Target backend for rendering (godot only for now)");
            var snapshotOutOption = new Option<DirectoryInfo?>("--out", "Output directory for snapshots (defaults to snapshots/ relative to manifest)");
            var snapshotGodotExeOption = new Option<FileInfo?>("--godot-exe", "Path to Godot executable (auto-detected if not specified)");
            var snapshotRunnerPathOption = new Option<FileInfo?>("--runner-path", "Path to SnapshotRunner.gd script (auto-detected if not specified)");
            var snapshotTimeoutOption = new Option<int>("--timeout", () => 60, "Timeout in seconds for Godot rendering");
            var snapshotVerboseOption = new Option<bool>("--verbose", "Enable verbose output");
            var snapshotDryRunOption = new Option<bool>("--dry-run", "Generate placeholder PNGs without invoking Godot (for testing pipeline)");

            snapshotCommand.AddOption(snapshotManifestOption);
            snapshotCommand.AddOption(snapshotStatesOption);
            snapshotCommand.AddOption(snapshotTargetOption);
            snapshotCommand.AddOption(snapshotOutOption);
            snapshotCommand.AddOption(snapshotGodotExeOption);
            snapshotCommand.AddOption(snapshotRunnerPathOption);
            snapshotCommand.AddOption(snapshotTimeoutOption);
            snapshotCommand.AddOption(snapshotVerboseOption);
            snapshotCommand.AddOption(snapshotDryRunOption);

            snapshotCommand.SetHandler((InvocationContext context) =>
            {
                var manifest = context.ParseResult.GetValueForOption(snapshotManifestOption);
                var states = context.ParseResult.GetValueForOption(snapshotStatesOption);
                var target = context.ParseResult.GetValueForOption(snapshotTargetOption) ?? "godot";
                var outDir = context.ParseResult.GetValueForOption(snapshotOutOption);
                var godotExe = context.ParseResult.GetValueForOption(snapshotGodotExeOption);
                var runnerPath = context.ParseResult.GetValueForOption(snapshotRunnerPathOption);
                var timeout = context.ParseResult.GetValueForOption(snapshotTimeoutOption);
                var verbose = context.ParseResult.GetValueForOption(snapshotVerboseOption);
                var dryRun = context.ParseResult.GetValueForOption(snapshotDryRunOption);

                context.ExitCode = HandleSnapshot(manifest, states, target, outDir, godotExe, runnerPath, timeout, verbose, dryRun);
            });

            // Video command
            var videoCommand = new Command("video", "Generate preview video from snapshots using Remotion");
            var videoSnapshotsOption = new Option<DirectoryInfo?>("--snapshots", "Directory containing snapshots and manifest (default: ui/snapshots)");
            var videoBaselineOption = new Option<DirectoryInfo?>("--baseline", "Directory containing baseline snapshots for comparison mode");
            var videoOutOption = new Option<FileInfo?>("--out", "Output video file (default: preview.mp4 or compare.mp4 in snapshots dir)");
            var videoFpsOption = new Option<int>("--fps", () => 30, "Frames per second");
            var videoSecondsOption = new Option<double>("--seconds-per-state", () => 1.5, "Duration per state in seconds");
            var videoTitleOption = new Option<bool>("--title-overlay", () => true, "Show state name overlay");
            var videoVerboseOption = new Option<bool>("--verbose", "Enable verbose output");

            videoCommand.AddOption(videoSnapshotsOption);
            videoCommand.AddOption(videoBaselineOption);
            videoCommand.AddOption(videoOutOption);
            videoCommand.AddOption(videoFpsOption);
            videoCommand.AddOption(videoSecondsOption);
            videoCommand.AddOption(videoTitleOption);
            videoCommand.AddOption(videoVerboseOption);

            videoCommand.SetHandler((InvocationContext context) =>
            {
                var snapshots = context.ParseResult.GetValueForOption(videoSnapshotsOption);
                var baseline = context.ParseResult.GetValueForOption(videoBaselineOption);
                var outFile = context.ParseResult.GetValueForOption(videoOutOption);
                var fps = context.ParseResult.GetValueForOption(videoFpsOption);
                var secondsPerState = context.ParseResult.GetValueForOption(videoSecondsOption);
                var titleOverlay = context.ParseResult.GetValueForOption(videoTitleOption);
                var verbose = context.ParseResult.GetValueForOption(videoVerboseOption);

                context.ExitCode = HandleVideo(snapshots, baseline, outFile, fps, secondsPerState, titleOverlay, verbose);
            });

            // Review command (convenience wrapper: snapshot + video + compare + diff)
            var reviewCommand = new Command("review", "Full review workflow: generate snapshots, preview video, and optionally compare against baseline");
            var reviewManifestOption = new Option<FileInfo?>("--manifest", "Compose manifest file (boom-hud.compose.json)");
            var reviewStatesOption = new Option<FileInfo?>("--states", "States manifest file (*.states.json)");
            var reviewOutOption = new Option<DirectoryInfo?>("--out", "Output directory for all artifacts (default: ui/)");
            var reviewBaselineOption = new Option<DirectoryInfo?>("--baseline", "Baseline directory for comparison (if omitted, no comparison)");
            var reviewDryRunOption = new Option<bool>("--dry-run", "Generate placeholder PNGs without invoking Godot");
            var reviewToleranceOption = new Option<int>("--tolerance", () => 8, "Per-channel delta tolerance for comparison");
            var reviewVerboseOption = new Option<bool>("--verbose", "Enable verbose output");

            reviewCommand.AddOption(reviewManifestOption);
            reviewCommand.AddOption(reviewStatesOption);
            reviewCommand.AddOption(reviewOutOption);
            reviewCommand.AddOption(reviewBaselineOption);
            reviewCommand.AddOption(reviewDryRunOption);
            reviewCommand.AddOption(reviewToleranceOption);
            reviewCommand.AddOption(reviewVerboseOption);

            reviewCommand.SetHandler((InvocationContext context) =>
            {
                var manifest = context.ParseResult.GetValueForOption(reviewManifestOption);
                var states = context.ParseResult.GetValueForOption(reviewStatesOption);
                var outDir = context.ParseResult.GetValueForOption(reviewOutOption);
                var baseline = context.ParseResult.GetValueForOption(reviewBaselineOption);
                var dryRun = context.ParseResult.GetValueForOption(reviewDryRunOption);
                var tolerance = context.ParseResult.GetValueForOption(reviewToleranceOption);
                var verbose = context.ParseResult.GetValueForOption(reviewVerboseOption);

                context.ExitCode = HandleReview(manifest, states, outDir, baseline, dryRun, tolerance, verbose);
            });

            // Baseline command (parent for subcommands)
            var baselineCommand = new Command("baseline", "Baseline comparison commands");

            // Baseline compare subcommand (extracted to BaselineCompareCommand)
            var baselineCompareCommand = BaselineCompareCommand.Build();

            // Baseline diff subcommand
            var baselineDiffCommand = new Command("diff", "Generate diff images for changed frames");
            var diffCurrentOption = new Option<DirectoryInfo?>("--current", "Current snapshots directory (with snapshots.manifest.json)");
            diffCurrentOption.AddAlias("-c");
            var diffBaselineOption = new Option<DirectoryInfo?>("--baseline", "Baseline snapshots directory (with snapshots.manifest.json)");
            diffBaselineOption.AddAlias("-b");
            var diffOutOption = new Option<DirectoryInfo?>("--out", "Output directory for diff images (default: ui/diffs)");
            diffOutOption.AddAlias("-o");
            var diffReportOption = new Option<FileInfo?>("--report", "Output diff report file (default: diff-report.json in output directory)");
            var diffToleranceOption = new Option<int>("--tolerance", () => 0, "Per-channel delta tolerance (0-255). Pixels within tolerance are considered unchanged.");
            var diffVerboseOption = new Option<bool>("--verbose", () => false, "Enable verbose output");

            baselineDiffCommand.AddOption(diffCurrentOption);
            baselineDiffCommand.AddOption(diffBaselineOption);
            baselineDiffCommand.AddOption(diffOutOption);
            baselineDiffCommand.AddOption(diffReportOption);
            baselineDiffCommand.AddOption(diffToleranceOption);
            baselineDiffCommand.AddOption(diffVerboseOption);

            baselineDiffCommand.SetHandler((InvocationContext context) =>
            {
                var currentDir = context.ParseResult.GetValueForOption(diffCurrentOption);
                var baselineDir = context.ParseResult.GetValueForOption(diffBaselineOption);
                var outDir = context.ParseResult.GetValueForOption(diffOutOption);
                var reportFile = context.ParseResult.GetValueForOption(diffReportOption);
                var tolerance = context.ParseResult.GetValueForOption(diffToleranceOption);
                var verbose = context.ParseResult.GetValueForOption(diffVerboseOption);

                context.ExitCode = HandleBaselineDiff(currentDir, baselineDir, outDir, reportFile, tolerance, verbose);
            });

            baselineCommand.AddCommand(baselineCompareCommand);
            baselineCommand.AddCommand(baselineDiffCommand);

            rootCommand.AddCommand(generateCommand);
            rootCommand.AddCommand(validateCommand);
            rootCommand.AddCommand(initCommand);
            rootCommand.AddCommand(uiproCommand);
            rootCommand.AddCommand(snapshotCommand);
            rootCommand.AddCommand(videoCommand);
            rootCommand.AddCommand(reviewCommand);
            rootCommand.AddCommand(baselineCommand);

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int HandleGenerate(
        FileInfo[] inputs,
        string? rootComponent,
        string? format,
        string target,
        DirectoryInfo? output,
        string @namespace,
        string? viewModelNamespace,
        bool noVmInterfaces,
        bool compose,
        bool tscn,
        bool tscnNoScript,
        string? contractId,
        string? node,
        FileInfo? annotations,
        FileInfo? variables,
        string? themeName,
        string? themeCollection,
        string? themeMode,
        string[]? descriptionReplacements,
        FileInfo? tokens)
    {
        // Validate all inputs exist
        foreach (var input in inputs)
        {
            if (!input.Exists)
            {
                throw new FileNotFoundException($"File not found: {input.FullName}");
            }
        }

        var outputRoot = output?.FullName ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputRoot);

        // Load all input documents
        var sourcedDocs = new List<SourcedDocument>();
        foreach (var input in inputs)
        {
            Console.WriteLine($"Loading input from: {input.FullName}");
            var json = File.ReadAllText(input.FullName);

            // Resolve format (autodetect from extension if not specified)
            var resolvedFormat = ResolveFormat(format, input.Name);
            Console.WriteLine($"Input format: {resolvedFormat}");

            var doc = LoadDocument(json, node, resolvedFormat);
            sourcedDocs.Add(new SourcedDocument(doc, new SourceIdentity(input.FullName)));
        }

        // Compose multiple documents if needed
        HudDocument document;
        if (sourcedDocs.Count == 1)
        {
            document = sourcedDocs[0].Document;
        }
        else
        {
            Console.WriteLine($"Composing {sourcedDocs.Count} documents...");
            var compositionResult = MultiSourceComposer.Compose(sourcedDocs, rootComponent);

            // Report composition diagnostics
            if (compositionResult.Diagnostics.Count > 0)
            {
                foreach (var diag in compositionResult.Diagnostics)
                {
                    if (diag.Severity == Abstractions.Diagnostics.DiagnosticSeverity.Error)
                        Console.Error.WriteLine(diag.ToString());
                    else
                        Console.WriteLine(diag.ToString());
                }
            }

            if (!compositionResult.Success || compositionResult.Document == null)
            {
                Console.Error.WriteLine("Composition failed. Fix errors before generating.");
                return 1;
            }

            document = compositionResult.Document;
            Console.WriteLine($"Composition successful: {document.Components.Count} components");
        }

        // Use first input for token discovery (primary source)
        var primaryInput = inputs[0];

        if (annotations != null)
        {
            if (!annotations.Exists)
            {
                throw new FileNotFoundException($"Annotations file not found: {annotations.FullName}");
            }

            Console.WriteLine($"Loading annotations from: {annotations.FullName}");
            var annotationDoc = FigmaAnnotations.LoadFile(annotations.FullName);
            document = FigmaAnnotations.Apply(document, annotationDoc);
        }

        // Load token registry (explicit file, or auto-discover ui/tokens.ir.json)
        var tokenRegistry = LoadTokenRegistry(tokens, primaryInput);
        if (tokenRegistry != null)
        {
            Console.WriteLine($"Token registry loaded: {tokenRegistry.Colors.Count} colors, {tokenRegistry.Spacing.Count} spacing, {tokenRegistry.Typography.Count} typography");

            // Resolve token references and collect diagnostics
            var tokenDiagnostics = ResolveTokenReferences(document, tokenRegistry, primaryInput.FullName);
            if (tokenDiagnostics.Count > 0)
            {
                foreach (var diag in tokenDiagnostics)
                {
                    if (diag.Severity == Abstractions.Diagnostics.DiagnosticSeverity.Error)
                        Console.Error.WriteLine(diag.ToString());
                    else
                        Console.WriteLine(diag.ToString());
                }

                // Hard fail on BH0102 (unresolved tokens)
                if (tokenDiagnostics.Any(d => d.Code == DiagnosticCodes.UnresolvedTokenRef))
                {
                    Console.Error.WriteLine("Token resolution failed. Fix unresolved token references before generating.");
                    return 1;
                }
            }
        }

        ThemeDocument? theme = null;
        if (variables != null)
        {
            if (!variables.Exists)
            {
                throw new FileNotFoundException($"Variables file not found: {variables.FullName}");
            }

            Console.WriteLine($"Loading Figma variables from: {variables.FullName}");
            var variablesJson = File.ReadAllText(variables.FullName);

            var resolvedThemeName = !string.IsNullOrWhiteSpace(themeName)
                ? themeName
                : Path.GetFileNameWithoutExtension(variables.Name);

            theme = FigmaThemeParser.Parse(
                variablesJson,
                themeName: resolvedThemeName,
                collectionName: themeCollection,
                mode: themeMode);
        }

        var options = new GenerationOptions
        {
            Namespace = @namespace,
            ViewModelNamespace = viewModelNamespace,
            EmitViewModelInterfaces = !noVmInterfaces,
            EmitCompose = compose,
            EmitTscn = tscn,
            EmitTscnAttachScript = !tscnNoScript,
            ContractId = contractId,
            OutputDirectory = outputRoot,
            Theme = theme,
            MissingCapabilityPolicy = MissingCapabilityPolicy.Warn,
            IncludeComments = true,
            UseNullableAnnotations = true,
            DescriptionReplacements = ParseDescriptionReplacements(descriptionReplacements)
        };

        var targets = ResolveTargets(target);
        foreach (var backend in targets)
        {
            var generator = CreateGenerator(backend);

            Console.WriteLine();
            Console.WriteLine($"=== Generating {backend} ===");
            var result = generator.Generate(document, options);

            if (result.Diagnostics.Count > 0)
            {
                foreach (var diag in result.Diagnostics)
                {
                    Console.WriteLine(diag.ToString());
                }
            }

            if (!result.Success)
            {
                Console.Error.WriteLine($"Generation failed for backend '{backend}'.");
                return 1;
            }

            var backendRoot = targets.Count > 1
                ? Path.Combine(outputRoot, backend)
                : outputRoot;

            WriteGeneratedFiles(backendRoot, result.Files);
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
        return 0;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Resolves the input format from explicit --format option or autodetects from filename.
    /// </summary>
    private static string ResolveFormat(string? explicitFormat, string filename)
    {
        if (!string.IsNullOrWhiteSpace(explicitFormat))
        {
            return explicitFormat.ToLowerInvariant() switch
            {
                "pen" or "pencil" => "pen",
                "figma" => "figma",
                "ir" => "ir",
                _ => throw new ArgumentException($"Unknown format: {explicitFormat}. Supported: pen, figma, ir")
            };
        }

        // Autodetect from extension
        var lower = filename.ToLowerInvariant();
        if (lower.EndsWith(".pen", StringComparison.Ordinal) || lower.EndsWith(".pen.json", StringComparison.Ordinal))
            return "pen";
        if (lower.EndsWith(".figma.json", StringComparison.Ordinal))
            return "figma";
        if (lower.EndsWith(".ir.json", StringComparison.Ordinal))
            return "ir";

        // Default to figma for backwards compatibility
        return "figma";
    }

    private static HudDocument LoadDocument(string json, string? nodeId, string format)
    {
        return format switch
        {
            "pen" => LoadPenDocument(json),
            "figma" => LoadFigmaDocument(json, nodeId),
            "ir" => LoadIrDocument(json),
            _ => throw new ArgumentException($"Unknown format: {format}")
        };
    }

    private static HudDocument LoadPenDocument(string json)
    {
        var parser = new PenParser();
        return parser.Parse(json);
    }

    private static HudDocument LoadFigmaDocument(string json, string? nodeId)
    {
        var parser = new FigmaParser();
        return string.IsNullOrWhiteSpace(nodeId)
            ? parser.Parse(json)
            : parser.ParseNode(json, nodeId);
    }

    private static HudDocument LoadIrDocument(string json)
    {
        var doc = JsonSerializer.Deserialize<HudDocument>(json, _jsonOptions);
        return doc ?? throw new InvalidOperationException("Failed to deserialize IR document");
    }

    private static Dictionary<string, string> ParseDescriptionReplacements(string[]? pairs)
    {
        if (pairs == null || pairs.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                continue;
            }

            var idx = pair.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0)
            {
                continue;
            }

            var from = pair[..idx];
            var to = pair[(idx + 1)..];

            if (string.IsNullOrWhiteSpace(from))
            {
                continue;
            }

            dict[from] = to;
        }

        return dict;
    }

    private static void HandleValidate(
        FileInfo input,
        string? node,
        FileInfo? variables,
        string? themeCollection,
        string? themeMode)
    {
        if (!input.Exists)
        {
            throw new FileNotFoundException($"File not found: {input.FullName}");
        }

        Console.WriteLine($"Validating: {input.FullName}");

        var json = File.ReadAllText(input.FullName);
        var parser = new FigmaParser();
        var validation = parser.Validate(json);

        if (!validation.IsValid)
        {
            var message = string.Join(Environment.NewLine, validation.Errors.Select(e => e.Message));
            throw new InvalidOperationException(message);
        }

        if (!string.IsNullOrWhiteSpace(node))
        {
            _ = parser.ParseNode(json, node);
        }

        if (variables != null)
        {
            if (!variables.Exists)
            {
                throw new FileNotFoundException($"Variables file not found: {variables.FullName}");
            }

            var variablesJson = File.ReadAllText(variables.FullName);
            _ = FigmaThemeParser.Parse(
                variablesJson,
                themeName: System.IO.Path.GetFileNameWithoutExtension(variables.Name),
                collectionName: themeCollection,
                mode: themeMode);
        }

        Console.WriteLine();
        Console.WriteLine("OK.");
    }

    private static void HandleInit(string name)
    {
        Console.WriteLine($"Initializing new BoomHud project: {name}");

        // TODO: Implement scaffolding
        Console.WriteLine();
        Console.WriteLine("⚠️  Project scaffolding not yet implemented. Coming in Phase 6!");
    }

    private static int HandleUiProMax(
        string query,
        string pythonExe,
        string? script,
        string? domain,
        string? stack,
        int maxResults,
        bool jsonOut,
        bool designSystem,
        string? projectName,
        string format,
        bool persist,
        string? page,
        DirectoryInfo? outputDir)
    {
        string scriptPath;
        if (!string.IsNullOrWhiteSpace(script))
        {
            scriptPath = script!;
        }
        else
        {
            var candidates = new[]
            {
                Path.Combine(".agent", "skills", "ui-ux-pro-max", "tooling", "scripts", "search.py"),
                Path.Combine(".agent", "skills", "ui-ux-pro-max", "scripts", "search.py"),
                Path.Combine("ref-projects", "ui-ux-pro-max-skill", "src", "ui-ux-pro-max", "scripts", "search.py")
            };

            scriptPath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine("UI/UX Pro Max tooling is not available (python script not found).");
            Console.Error.WriteLine("Provide an explicit path via --script (and optionally --python).");
            return 1;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                UseShellExecute = false,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(query);

            if (designSystem)
            {
                psi.ArgumentList.Add("--design-system");
            }

            if (!string.IsNullOrWhiteSpace(domain))
            {
                psi.ArgumentList.Add("--domain");
                psi.ArgumentList.Add(domain!);
            }

            if (!string.IsNullOrWhiteSpace(stack))
            {
                psi.ArgumentList.Add("--stack");
                psi.ArgumentList.Add(stack!);
            }

            if (maxResults != 3)
            {
                psi.ArgumentList.Add("--max-results");
                psi.ArgumentList.Add(maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (jsonOut)
            {
                psi.ArgumentList.Add("--json");
            }

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                psi.ArgumentList.Add("--project-name");
                psi.ArgumentList.Add(projectName!);
            }

            if (!string.IsNullOrWhiteSpace(format))
            {
                psi.ArgumentList.Add("--format");
                psi.ArgumentList.Add(format);
            }

            if (persist)
            {
                psi.ArgumentList.Add("--persist");
            }

            if (!string.IsNullOrWhiteSpace(page))
            {
                psi.ArgumentList.Add("--page");
                psi.ArgumentList.Add(page!);
            }

            if (outputDir != null)
            {
                psi.ArgumentList.Add("--output-dir");
                psi.ArgumentList.Add(outputDir.FullName);
            }

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Console.Error.WriteLine($"Failed to start process: {pythonExe}");
                return 1;
            }

            proc.WaitForExit();
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static IReadOnlyList<string> ResolveTargets(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return ["TerminalGui"];
        }

        var normalized = target.Trim().ToLowerInvariant();
        return normalized switch
        {
            "terminalgui" or "terminal-gui" or "terminal" => ["TerminalGui"],
            "avalonia" => ["Avalonia"],
            "godot" => ["Godot"],
            "all" => ["TerminalGui", "Avalonia", "Godot"],
            _ => throw new ArgumentException($"Unknown target: {target}")
        };
    }

    private static IBackendGenerator CreateGenerator(string backend)
    {
        return backend switch
        {
            "TerminalGui" => new TerminalGuiGenerator(),
            "Avalonia" => new AvaloniaGenerator(),
            "Godot" => new GodotGenerator(),
            _ => throw new ArgumentException($"Unknown backend: {backend}")
        };
    }

    private static void WriteGeneratedFiles(string outputRoot, IReadOnlyList<GeneratedFile> files)
    {
        Directory.CreateDirectory(outputRoot);

        foreach (var file in files)
        {
            var targetPath = System.IO.Path.Combine(outputRoot, file.Path);
            var dir = System.IO.Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(targetPath, file.Content);
            Console.WriteLine($"Wrote: {targetPath}");
        }
    }

    /// <summary>
    /// Loads token registry from explicit file or auto-discovers ui/tokens.ir.json.
    /// </summary>
    private static TokenRegistry? LoadTokenRegistry(FileInfo? explicitTokensFile, FileInfo inputFile)
    {
        // Explicit --tokens file
        if (explicitTokensFile != null)
        {
            if (!explicitTokensFile.Exists)
            {
                throw new FileNotFoundException($"Token registry file not found: {explicitTokensFile.FullName}");
            }
            Console.WriteLine($"Loading tokens from: {explicitTokensFile.FullName}");
            return TokenRegistry.LoadFromFile(explicitTokensFile.FullName);
        }

        // Auto-discover: look for ui/tokens.ir.json relative to input file
        var inputDir = inputFile.Directory?.FullName ?? Directory.GetCurrentDirectory();
        var autoTokensPath = System.IO.Path.Combine(inputDir, "ui", "tokens.ir.json");
        if (File.Exists(autoTokensPath))
        {
            Console.WriteLine($"Auto-discovered tokens: {autoTokensPath}");
            return TokenRegistry.LoadFromFile(autoTokensPath);
        }

        // Also check parent/ui/tokens.ir.json (common layout)
        var parentDir = inputFile.Directory?.Parent?.FullName;
        if (parentDir != null)
        {
            var parentTokensPath = System.IO.Path.Combine(parentDir, "ui", "tokens.ir.json");
            if (File.Exists(parentTokensPath))
            {
                Console.WriteLine($"Auto-discovered tokens: {parentTokensPath}");
                return TokenRegistry.LoadFromFile(parentTokensPath);
            }
        }

        // No tokens file found - this is OK, just means no token resolution
        return null;
    }

    /// <summary>
    /// Validates token references in the document. Returns diagnostics (errors/warnings).
    /// Does NOT modify the document - generators should resolve tokens at generation time.
    /// </summary>
    private static List<BoomHudDiagnostic> ResolveTokenReferences(HudDocument document, TokenRegistry registry, string sourceFile)
    {
        var diagnostics = new List<BoomHudDiagnostic>();

        // Validate tokens in the root component tree
        ValidateComponentTokens(document.Root, registry, sourceFile, diagnostics);

        // Also validate tokens in component definitions
        foreach (var (_, compDef) in document.Components)
        {
            if (compDef.Root != null)
            {
                ValidateComponentTokens(compDef.Root, registry, sourceFile, diagnostics);
            }
        }

        return diagnostics;
    }

    private static void ValidateComponentTokens(ComponentNode component, TokenRegistry registry, string sourceFile, List<BoomHudDiagnostic> diagnostics)
    {
        var nodeId = component.Id ?? "(anonymous)";

        var style = component.Style;
        if (style != null)
        {
            // Validate color tokens
            ValidateTokenRef(style.BackgroundToken, TokenCategory.Color, "BackgroundToken", nodeId, registry, sourceFile, diagnostics);
            ValidateTokenRef(style.ForegroundToken, TokenCategory.Color, "ForegroundToken", nodeId, registry, sourceFile, diagnostics);
            ValidateTokenRef(style.BorderColorToken, TokenCategory.Color, "BorderColorToken", nodeId, registry, sourceFile, diagnostics);

            // Warn on inline color values (when no token is used)
            WarnInlineColor(style.Background?.ToString(), style.BackgroundToken, "Background", nodeId, sourceFile, diagnostics);
            WarnInlineColor(style.Foreground?.ToString(), style.ForegroundToken, "Foreground", nodeId, sourceFile, diagnostics);
        }

        // Process children
        foreach (var child in component.Children)
        {
            ValidateComponentTokens(child, registry, sourceFile, diagnostics);
        }
    }

    private static void ValidateTokenRef(string? tokenRef, TokenCategory expectedCategory, string fieldName, string nodeId, TokenRegistry registry, string sourceFile, List<BoomHudDiagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(tokenRef))
            return;

        var resolved = registry.TryResolve(tokenRef);
        if (resolved == null)
        {
            diagnostics.Add(Diagnostics.UnresolvedTokenRef(tokenRef, sourceFile, nodeId));
            return;
        }

        // BH0103: Warn if token is deprecated
        if (resolved.Deprecated)
        {
            diagnostics.Add(Diagnostics.DeprecatedToken(tokenRef, sourceFile, nodeId));
        }

        if (resolved.Category != expectedCategory)
        {
            diagnostics.Add(new BoomHudDiagnostic(
                DiagnosticCodes.TokenCategoryMismatch,
                Abstractions.Diagnostics.DiagnosticSeverity.Warning,
                $"Token '{tokenRef}' is a {resolved.Category} token but used as {expectedCategory}",
                sourceFile,
                nodeId));
        }
    }

    private static void WarnInlineColor(string? value, string? tokenRef, string fieldName, string nodeId, string sourceFile, List<BoomHudDiagnostic> diagnostics)
    {
        // Don't warn if a token is used or if the value is empty
        if (!string.IsNullOrEmpty(tokenRef) || string.IsNullOrEmpty(value))
            return;

        // Only warn for explicit color values (hex, rgb, etc.)
        if (value.StartsWith('#') ||
            value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Diagnostics.InlineTokenWarning(value, fieldName, sourceFile, nodeId));
        }
    }

    private static int HandleSnapshot(
        FileInfo? manifest,
        FileInfo? states,
        string target,
        DirectoryInfo? outDir,
        FileInfo? godotExe,
        FileInfo? runnerPathExplicit,
        int timeout,
        bool verbose,
        bool dryRun)
    {
        try
        {
            // Validate target
            if (!string.Equals(target, "godot", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: Snapshot rendering currently only supports 'godot' target (got: {target})");
                return 1;
            }

            // Load manifest (required for now to find generated scene)
            if (manifest == null)
            {
                Console.Error.WriteLine("Error: --manifest is required for snapshot generation");
                return 1;
            }
            if (!manifest.Exists)
            {
                Console.Error.WriteLine($"Error: Manifest file not found: {manifest.FullName}");
                return 1;
            }

            var composeManifest = ComposeManifest.LoadFromFile(manifest.FullName);

            // Resolve states file
            var statesPath = states?.FullName;
            if (statesPath == null)
            {
                // Default: look for ui/states/*.states.json relative to manifest
                var manifestDir = Path.GetDirectoryName(manifest.FullName) ?? ".";
                var statesDir = Path.Combine(manifestDir, "states");
                if (Directory.Exists(statesDir))
                {
                    var statesFiles = Directory.GetFiles(statesDir, "*.states.json");
                    if (statesFiles.Length == 1)
                    {
                        statesPath = statesFiles[0];
                    }
                    else if (statesFiles.Length > 1)
                    {
                        Console.Error.WriteLine($"Error: Multiple .states.json files found in {statesDir}. Use --states to specify one.");
                        return 1;
                    }
                }
            }

            if (statesPath == null || !File.Exists(statesPath))
            {
                Console.Error.WriteLine("Error: States file not found. Use --states to specify a .states.json file.");
                return 1;
            }

            Console.WriteLine($"Loading states: {statesPath}");
            var statesManifest = SnapshotStatesManifest.LoadFromFile(statesPath);

            if (statesManifest.States.Count == 0)
            {
                Console.Error.WriteLine("Error: States manifest contains no states to render");
                return 1;
            }

            // Resolve output directory
            var outputPath = outDir?.FullName;
            if (outputPath == null)
            {
                var manifestDir = Path.GetDirectoryName(manifest.FullName) ?? ".";
                outputPath = Path.Combine(manifestDir, "snapshots");
            }
            Directory.CreateDirectory(outputPath);

            Console.WriteLine($"Output directory: {outputPath}");
            Console.WriteLine($"Viewport: {statesManifest.Viewport.Width}x{statesManifest.Viewport.Height} @ {statesManifest.Viewport.Scale}x");
            Console.WriteLine($"States to render: {statesManifest.States.Count}");

            if (dryRun)
            {
                Console.WriteLine("Mode: dry-run (generating placeholder PNGs without Godot)");
            }

            // Find Godot executable (not required for dry-run)
            string? godotPath = null;
            if (!dryRun)
            {
                godotPath = ResolveGodotExecutable(godotExe);
                if (godotPath == null)
                {
                    Console.Error.WriteLine("Error: Could not find Godot executable. Use --godot-exe to specify path, or use --dry-run for testing.");
                    return 1;
                }

                if (verbose)
                {
                    Console.WriteLine($"Godot executable: {godotPath}");
                }
            }

            // Generate snapshots
            var outputManifest = new SnapshotOutputManifest
            {
                Target = target,
                Viewport = statesManifest.Viewport,
                ToolVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                GodotVersion = dryRun ? null : CaptureGodotVersion(godotPath),
                RunnerInfo = CaptureRunnerInfo(dryRun)
            };
            var snapshots = new List<SnapshotFileInfo>();
            var inputHashes = new Dictionary<string, string>();

            // Hash the input states file
            inputHashes["states"] = ComputeFileHash(statesPath);

            // Hash the compose manifest
            inputHashes["manifest"] = ComputeFileHash(manifest.FullName);

            // Hash each source file from the manifest
            var sourcePaths = composeManifest.ResolveSourcePaths(manifest.FullName);
            foreach (var sourcePath in sourcePaths)
            {
                if (File.Exists(sourcePath))
                {
                    var sourceKey = $"source:{Path.GetFileName(sourcePath)}";
                    inputHashes[sourceKey] = ComputeFileHash(sourcePath);
                }
            }

            if (dryRun)
            {
                // Dry-run: generate placeholder PNGs for each state
                for (int i = 0; i < statesManifest.States.Count; i++)
                {
                    var state = statesManifest.States[i];
                    var fileName = $"{i:D3}_{SanitizeFileName(state.Name)}.png";
                    var outputFilePath = Path.Combine(outputPath, fileName);

                    Console.WriteLine($"  [{i + 1}/{statesManifest.States.Count}] Rendering: {state.Name}...");

                    var placeholderColor = state.Name.GetHashCode();
                    CreatePlaceholderPng(outputFilePath, statesManifest.Viewport.Width, statesManifest.Viewport.Height, placeholderColor);

                    if (verbose)
                    {
                        Console.WriteLine($"    [dry-run] Created placeholder PNG");
                    }

                    if (File.Exists(outputFilePath))
                    {
                        var hash = ComputeFileHash(outputFilePath);
                        snapshots.Add(new SnapshotFileInfo
                        {
                            State = state.Name,
                            Path = fileName,
                            Sha256 = hash
                        });

                        if (verbose)
                        {
                            Console.WriteLine($"    SHA256: {hash}");
                        }
                    }
                }
            }
            else
            {
                // Real rendering: invoke Godot headless to render all states at once
                Console.WriteLine("Invoking Godot headless renderer...");

                var scenePath = ResolveScenePath(composeManifest, manifest.FullName);
                if (scenePath == null)
                {
                    Console.Error.WriteLine("Error: Could not resolve scene path. Ensure generation has run first.");
                    return 1;
                }

                var runnerPath = ResolveSnapshotRunnerPath(runnerPathExplicit);
                if (runnerPath == null)
                {
                    Console.Error.WriteLine("Error: Could not find SnapshotRunner.gd. Use --runner-path to specify path, or ensure it exists in the godot/ directory.");
                    return 1;
                }

                if (verbose)
                {
                    Console.WriteLine($"Runner script: {runnerPath}");
                }

                // Hash the runner script for manifest
                inputHashes["runner"] = ComputeFileHash(runnerPath);

                var exitCode = InvokeGodotHeadless(
                    godotPath!,
                    runnerPath,
                    scenePath,
                    statesPath,
                    outputPath,
                    timeout,
                    verbose);

                if (exitCode != 0)
                {
                    Console.Error.WriteLine($"Error: Godot headless rendering failed (exit code: {exitCode})");
                    return exitCode;
                }

                // Collect generated files
                for (int i = 0; i < statesManifest.States.Count; i++)
                {
                    var state = statesManifest.States[i];
                    var fileName = $"{i:D3}_{SanitizeFileName(state.Name)}.png";
                    var outputFilePath = Path.Combine(outputPath, fileName);

                    if (File.Exists(outputFilePath))
                    {
                        var hash = ComputeFileHash(outputFilePath);
                        snapshots.Add(new SnapshotFileInfo
                        {
                            State = state.Name,
                            Path = fileName,
                            Sha256 = hash
                        });

                        if (verbose)
                        {
                            Console.WriteLine($"  {state.Name}: SHA256 = {hash}");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Warning: Expected output file not found: {outputFilePath}");
                    }
                }
            }

            // Write output manifest
            outputManifest = outputManifest with
            {
                InputHashes = inputHashes,
                Snapshots = snapshots
            };

            var manifestOutputPath = Path.Combine(outputPath, "snapshots.manifest.json");
            File.WriteAllText(manifestOutputPath, outputManifest.ToJson());
            Console.WriteLine($"Manifest written: {manifestOutputPath}");

            Console.WriteLine($"✓ Generated {snapshots.Count} snapshots");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int HandleVideo(
        DirectoryInfo? snapshotsDir,
        DirectoryInfo? baselineDir,
        FileInfo? outFile,
        int fps,
        double secondsPerState,
        bool titleOverlay,
        bool verbose)
    {
        try
        {
            var isCompareMode = baselineDir != null;

            // Resolve snapshots directory
            var snapshotsPath = snapshotsDir?.FullName;
            if (snapshotsPath == null)
            {
                // Default to ui/snapshots relative to current directory
                snapshotsPath = Path.Combine(Environment.CurrentDirectory, "ui", "snapshots");
            }

            if (!Directory.Exists(snapshotsPath))
            {
                Console.Error.WriteLine($"Error: Snapshots directory not found: {snapshotsPath}");
                return 1;
            }

            // Check for manifest
            var manifestPath = Path.Combine(snapshotsPath, "snapshots.manifest.json");
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine($"Error: Manifest not found: {manifestPath}");
                Console.Error.WriteLine("Run 'boomhud snapshot' first to generate snapshots.");
                return 1;
            }

            // Validate baseline directory if provided
            string? baselinePath = null;
            if (isCompareMode)
            {
                baselinePath = baselineDir!.FullName;
                if (!Directory.Exists(baselinePath))
                {
                    Console.Error.WriteLine($"Error: Baseline directory not found: {baselinePath}");
                    return 1;
                }

                var baselineManifestPath = Path.Combine(baselinePath, "snapshots.manifest.json");
                if (!File.Exists(baselineManifestPath))
                {
                    Console.Error.WriteLine($"Error: Baseline manifest not found: {baselineManifestPath}");
                    return 1;
                }
            }

            // Resolve output path (default differs for compare mode)
            var defaultFileName = isCompareMode ? "compare.mp4" : "preview.mp4";
            var outputPath = outFile?.FullName ?? Path.Combine(snapshotsPath, defaultFileName);

            Console.WriteLine($"Mode: {(isCompareMode ? "comparison" : "preview")}");
            Console.WriteLine($"Snapshots directory: {snapshotsPath}");
            if (isCompareMode)
            {
                Console.WriteLine($"Baseline directory: {baselinePath}");
            }
            Console.WriteLine($"Output: {outputPath}");

            // Find Remotion project
            var remotionDir = ResolveRemotionDir();
            if (remotionDir == null)
            {
                Console.Error.WriteLine("Error: Could not find Remotion project directory.");
                Console.Error.WriteLine("Ensure the remotion/ directory exists with package.json.");
                return 1;
            }

            if (verbose)
            {
                Console.WriteLine($"Remotion project: {remotionDir}");
            }

            // Check if node_modules exists, if not suggest npm install
            var nodeModulesPath = Path.Combine(remotionDir, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                Console.Error.WriteLine("Error: Remotion dependencies not installed.");
                Console.Error.WriteLine($"Run: cd {remotionDir} && npm install");
                return 1;
            }

            // Invoke Remotion render script
            var exitCode = InvokeRemotionRender(
                remotionDir,
                snapshotsPath,
                baselinePath,
                outputPath,
                fps,
                secondsPerState,
                titleOverlay,
                verbose);

            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: Video rendering failed (exit code: {exitCode})");
                return exitCode;
            }

            Console.WriteLine($"✓ Video generated: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int HandleReview(
        FileInfo? manifestFile,
        FileInfo? statesFile,
        DirectoryInfo? outDir,
        DirectoryInfo? baselineDir,
        bool dryRun,
        int tolerance,
        bool verbose)
    {
        try
        {
            // Resolve output directory
            var outputRoot = outDir?.FullName ?? Path.Combine(Environment.CurrentDirectory, "ui");
            var snapshotsPath = Path.Combine(outputRoot, "snapshots");
            var diffsPath = Path.Combine(outputRoot, "diffs");
            var reportPath = Path.Combine(outputRoot, "baseline-report.json");

            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║         BoomHud Review Workflow          ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.WriteLine();

            // Step 1: Generate snapshots
            Console.WriteLine("▶ Step 1: Generating snapshots...");
            var snapshotResult = HandleSnapshot(
                manifestFile,
                statesFile,
                "godot",
                new DirectoryInfo(snapshotsPath),
                godotExe: null,
                runnerPathExplicit: null,
                timeout: 60,
                verbose,
                dryRun);

            if (snapshotResult != 0)
            {
                Console.Error.WriteLine("✗ Snapshot generation failed");
                return snapshotResult;
            }
            Console.WriteLine("✓ Snapshots generated");
            Console.WriteLine();

            // Step 2: Generate preview video
            Console.WriteLine("▶ Step 2: Generating preview video...");
            var videoResult = HandleVideo(
                new DirectoryInfo(snapshotsPath),
                baselineDir: null,
                outFile: null,
                fps: 30,
                secondsPerState: 1.5,
                titleOverlay: true,
                verbose);

            if (videoResult != 0)
            {
                Console.WriteLine("⚠ Preview video generation failed (continuing...)");
            }
            else
            {
                Console.WriteLine("✓ Preview video generated");
            }
            Console.WriteLine();

            // Step 3: Baseline comparison (if baseline provided)
            if (baselineDir != null && baselineDir.Exists)
            {
                Console.WriteLine("▶ Step 3: Comparing with baseline...");
                var compareResult = HandleBaselineCompare(
                    new DirectoryInfo(snapshotsPath),
                    baselineDir,
                    new FileInfo(reportPath),
                    printSummary: true,
                    failOnChanged: false,
                    failOn: null,
                    tolerance,
                    minChangedPercent: 0.01,
                    protectedFrames: Array.Empty<string>(),
                    ghActions: false,
                    verbose);

                if (compareResult != 0)
                {
                    Console.WriteLine("⚠ Baseline comparison reported differences");
                }
                else
                {
                    Console.WriteLine("✓ Baseline comparison complete");
                }
                Console.WriteLine();

                // Step 4: Generate diff images
                Console.WriteLine("▶ Step 4: Generating diff images...");
                var diffResult = HandleBaselineDiff(
                    new DirectoryInfo(snapshotsPath),
                    baselineDir,
                    new DirectoryInfo(diffsPath),
                    reportFile: null,
                    tolerance,
                    verbose);

                if (diffResult != 0)
                {
                    Console.WriteLine("⚠ Diff image generation failed");
                }
                else
                {
                    Console.WriteLine("✓ Diff images generated");
                }
                Console.WriteLine();

                // Step 5: Generate comparison video
                Console.WriteLine("▶ Step 5: Generating comparison video...");
                var compareVideoResult = HandleVideo(
                    new DirectoryInfo(snapshotsPath),
                    baselineDir,
                    outFile: null,
                    fps: 30,
                    secondsPerState: 1.5,
                    titleOverlay: true,
                    verbose);

                if (compareVideoResult != 0)
                {
                    Console.WriteLine("⚠ Comparison video generation failed");
                }
                else
                {
                    Console.WriteLine("✓ Comparison video generated");
                }
            }
            else
            {
                Console.WriteLine("ℹ No baseline provided, skipping comparison steps");
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════════");
            Console.WriteLine("Review complete! Outputs:");
            Console.WriteLine($"  Snapshots:     {snapshotsPath}");
            Console.WriteLine($"  Preview video: {Path.Combine(snapshotsPath, "preview.mp4")}");
            if (baselineDir != null && baselineDir.Exists)
            {
                Console.WriteLine($"  Compare video: {Path.Combine(snapshotsPath, "compare.mp4")}");
                Console.WriteLine($"  Report:        {reportPath}");
                Console.WriteLine($"  Diff images:   {diffsPath}");
            }
            Console.WriteLine("════════════════════════════════════════════");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string? ResolveRemotionDir()
    {
        // Look for remotion/ directory relative to CLI executable or current directory
        var exeDir = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? ".";

        var candidates = new[]
        {
            Path.Combine(exeDir, "remotion"),
            Path.Combine(exeDir, "..", "..", "..", "..", "remotion"),
            Path.Combine(Environment.CurrentDirectory, "remotion"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            var packageJson = Path.Combine(fullPath, "package.json");
            if (File.Exists(packageJson))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static int InvokeRemotionRender(
        string remotionDir,
        string snapshotsPath,
        string? baselinePath,
        string outputPath,
        int fps,
        double secondsPerState,
        bool titleOverlay,
        bool verbose)
    {
        // Build arguments for render.ts
        var args = new List<string>
        {
            "npx",
            "ts-node",
            "render.ts",
            "--snapshots", snapshotsPath,
            "--out", outputPath,
            "--fps", fps.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--seconds-per-state", secondsPerState.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--title-overlay", titleOverlay ? "on" : "off"
        };

        // Add baseline for comparison mode
        if (!string.IsNullOrEmpty(baselinePath))
        {
            args.AddRange(["--baseline", baselinePath]);
        }

        if (verbose)
        {
            Console.WriteLine($"Running: {string.Join(" ", args)}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "sh",
            Arguments = OperatingSystem.IsWindows()
                ? $"/c {string.Join(" ", args)}"
                : $"-c \"{string.Join(" ", args)}\"",
            WorkingDirectory = remotionDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                if (verbose)
                {
                    Console.WriteLine($"[Remotion] {e.Data}");
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Console.Error.WriteLine($"[Remotion:err] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Allow up to 5 minutes for video rendering
        var exited = process.WaitForExit(300_000);

        if (!exited)
        {
            Console.Error.WriteLine("Error: Remotion process timed out");
            try { process.Kill(); } catch { }
            return -1;
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Adapter method that delegates to BaselineCompareHandler.
    /// Used by internal callers (e.g., HandleReview) that still use the old signature.
    /// </summary>
    private static int HandleBaselineCompare(
        DirectoryInfo? currentDir,
        DirectoryInfo? baselineDir,
        FileInfo? outFile,
        bool printSummary,
        bool failOnChanged,
        string? failOn,
        int tolerance,
        double minChangedPercent,
        string[] protectedFrames,
        bool ghActions,
        bool verbose)
    {
        var (failOnMode, failPercent) = BaselineCompareOptions.ParseFailOn(failOn, failOnChanged);

        var options = new BaselineCompareOptions
        {
            CurrentDir = currentDir,
            BaselineDir = baselineDir,
            OutFile = outFile,
            PrintSummary = printSummary,
            GhActions = ghActions,
            FailOn = failOnMode,
            FailPercent = failPercent,
            Tolerance = tolerance,
            MinChangedPercent = minChangedPercent,
            ProtectedFrames = protectedFrames,
            Verbose = verbose
        };

        return BaselineCompareHandler.Execute(options);
    }

    private static SnapshotOutputManifest? LoadManifest(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SnapshotOutputManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    private static BaselineReport CompareManifests(
        SnapshotOutputManifest current,
        SnapshotOutputManifest baseline,
        string currentPath,
        string baselinePath,
        string currentManifestPath,
        string baselineManifestPath)
    {
        var frames = new List<FrameCompareResult>();

        // Build lookup for baseline by state name
        var baselineLookup = baseline.Snapshots.ToDictionary(s => s.State, s => s);
        var currentLookup = current.Snapshots.ToDictionary(s => s.State, s => s);

        // Union of all names
        var allNames = baselineLookup.Keys.Union(currentLookup.Keys).OrderBy(n => n).ToList();

        // Check compatibility (Godot version mismatch makes changes non-actionable)
        var compatible = true;
        string? incompatibilityReason = null;
        var isGodotVersionMismatch = false;

        if (!string.IsNullOrEmpty(baseline.GodotVersion) &&
            !string.IsNullOrEmpty(current.GodotVersion) &&
            baseline.GodotVersion != current.GodotVersion)
        {
            // Different Godot versions - mark as incompatible
            compatible = false;
            isGodotVersionMismatch = true;
            incompatibilityReason = $"Godot version mismatch: baseline={baseline.GodotVersion}, current={current.GodotVersion}";
        }

        var index = 0;
        var unchanged = 0;
        var changed = 0;
        var changedNonActionable = 0;
        var missingBaseline = 0;
        var missingCurrent = 0;

        foreach (var name in allNames)
        {
            var hasBaseline = baselineLookup.TryGetValue(name, out var baselineSnap);
            var hasCurrent = currentLookup.TryGetValue(name, out var currentSnap);

            FrameCompareStatus status;
            var actionable = true;

            if (hasBaseline && hasCurrent)
            {
                if (baselineSnap!.Sha256 == currentSnap!.Sha256)
                {
                    status = FrameCompareStatus.Unchanged;
                    unchanged++;
                }
                else
                {
                    // Hash differs - check if actionable
                    if (isGodotVersionMismatch)
                    {
                        status = FrameCompareStatus.ChangedNonActionable;
                        actionable = false;
                        changedNonActionable++;
                    }
                    else
                    {
                        status = FrameCompareStatus.Changed;
                        changed++;
                    }
                }
            }
            else if (!hasBaseline)
            {
                status = FrameCompareStatus.MissingBaseline;
                missingBaseline++;
            }
            else
            {
                status = FrameCompareStatus.MissingCurrent;
                missingCurrent++;
            }

            frames.Add(new FrameCompareResult
            {
                Index = index++,
                Name = name,
                Status = status,
                Actionable = actionable,
                BaselineHash = baselineSnap?.Sha256,
                CurrentHash = currentSnap?.Sha256,
                BaselinePath = baselineSnap?.Path,
                CurrentPath = currentSnap?.Path
            });
        }

        return new BaselineReport
        {
            ToolVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
            Baseline = new ManifestInfo
            {
                Path = baselineManifestPath,
                GodotVersion = baseline.GodotVersion,
                ToolVersion = baseline.ToolVersion,
                Target = baseline.Target,
                InputHash = baseline.InputHashes.TryGetValue("manifest", out var baselineHash) ? baselineHash : null,
                SnapshotCount = baseline.Snapshots.Count
            },
            Current = new ManifestInfo
            {
                Path = currentManifestPath,
                GodotVersion = current.GodotVersion,
                ToolVersion = current.ToolVersion,
                Target = current.Target,
                InputHash = current.InputHashes.TryGetValue("manifest", out var manifestHash) ? manifestHash : null,
                SnapshotCount = current.Snapshots.Count
            },
            Summary = new BaselineCompareSummary
            {
                Total = frames.Count,
                Unchanged = unchanged,
                Changed = changed,
                ChangedNonActionable = changedNonActionable,
                MissingBaseline = missingBaseline,
                MissingCurrent = missingCurrent,
                Compatible = compatible,
                IncompatibilityReason = incompatibilityReason
            },
            Frames = frames
        };
    }

    private static int HandleBaselineDiff(
        DirectoryInfo? currentDir,
        DirectoryInfo? baselineDir,
        DirectoryInfo? outDir,
        FileInfo? reportFile,
        int tolerance,
        bool verbose)
    {
        try
        {
            // Resolve current snapshots directory
            var currentPath = currentDir?.FullName ?? Path.Combine(Environment.CurrentDirectory, "ui", "snapshots");
            if (!Directory.Exists(currentPath))
            {
                Console.Error.WriteLine($"Error: Current snapshots directory not found: {currentPath}");
                return 1;
            }

            var currentManifestPath = Path.Combine(currentPath, "snapshots.manifest.json");
            if (!File.Exists(currentManifestPath))
            {
                Console.Error.WriteLine($"Error: Current manifest not found: {currentManifestPath}");
                return 1;
            }

            // Resolve baseline directory
            var baselinePath = baselineDir?.FullName;
            if (string.IsNullOrEmpty(baselinePath))
            {
                Console.Error.WriteLine("Error: --baseline directory is required");
                return 1;
            }

            if (!Directory.Exists(baselinePath))
            {
                Console.Error.WriteLine($"Error: Baseline directory not found: {baselinePath}");
                return 1;
            }

            var baselineManifestPath = Path.Combine(baselinePath, "snapshots.manifest.json");
            if (!File.Exists(baselineManifestPath))
            {
                Console.Error.WriteLine($"Error: Baseline manifest not found: {baselineManifestPath}");
                return 1;
            }

            // Load both manifests
            var currentManifest = LoadManifest(currentManifestPath);
            var baselineManifest = LoadManifest(baselineManifestPath);

            if (currentManifest == null || baselineManifest == null)
            {
                Console.Error.WriteLine("Error: Could not parse manifests");
                return 1;
            }

            // Resolve output directory
            var outputPath = outDir?.FullName ?? Path.Combine(Environment.CurrentDirectory, "ui", "diffs");
            Directory.CreateDirectory(outputPath);

            Console.WriteLine($"Current: {currentPath}");
            Console.WriteLine($"Baseline: {baselinePath}");
            Console.WriteLine($"Output: {outputPath}");
            if (tolerance > 0) Console.WriteLine($"Tolerance: {tolerance}");

            // Compare and generate diffs
            var report = CompareManifests(currentManifest, baselineManifest, currentPath, baselinePath, currentManifestPath, baselineManifestPath);
            var diffResults = GenerateDiffImages(report, currentPath, baselinePath, outputPath, tolerance, verbose);

            // Update report with diff paths
            var updatedFrames = report.Frames.Select((f, i) => f with { DiffPath = diffResults.GetValueOrDefault(f.Name) }).ToList();
            var updatedReport = report with { Frames = updatedFrames };

            // Write report
            var reportPath = reportFile?.FullName ?? Path.Combine(outputPath, "diff-report.json");
            File.WriteAllText(reportPath, updatedReport.ToJson());
            Console.WriteLine($"Report written: {reportPath}");

            // Summary
            var diffCount = diffResults.Count;
            Console.WriteLine($"✓ Generated {diffCount} diff images");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static Dictionary<string, string> GenerateDiffImages(
        BaselineReport report,
        string currentPath,
        string baselinePath,
        string outputPath,
        int tolerance,
        bool verbose)
    {
        var diffPaths = new Dictionary<string, string>();

        // Only process Changed and ChangedNonActionable frames
        var changedFrames = report.Frames
            .Where(f => f.Status == FrameCompareStatus.Changed || f.Status == FrameCompareStatus.ChangedNonActionable)
            .ToList();

        if (changedFrames.Count == 0)
        {
            Console.WriteLine("No changed frames to diff");
            return diffPaths;
        }

        Console.WriteLine($"Generating diffs for {changedFrames.Count} changed frames...");

        foreach (var frame in changedFrames)
        {
            if (string.IsNullOrEmpty(frame.BaselinePath) || string.IsNullOrEmpty(frame.CurrentPath))
            {
                continue;
            }

            var baselineFile = Path.Combine(baselinePath, frame.BaselinePath);
            var currentFile = Path.Combine(currentPath, frame.CurrentPath);

            if (!File.Exists(baselineFile) || !File.Exists(currentFile))
            {
                if (verbose)
                {
                    Console.WriteLine($"  Skipping {frame.Name}: missing file");
                }
                continue;
            }

            // Generate file names with index prefix for deterministic ordering
            var prefix = frame.Index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            var safeName = SanitizeFileName(frame.Name);
            var baselineOutName = $"{prefix}_{safeName}__baseline.png";
            var currentOutName = $"{prefix}_{safeName}__current.png";
            var diffOutName = $"{prefix}_{safeName}__diff.png";

            var baselineOutPath = Path.Combine(outputPath, baselineOutName);
            var currentOutPath = Path.Combine(outputPath, currentOutName);
            var diffOutPath = Path.Combine(outputPath, diffOutName);

            try
            {
                // Copy baseline and current
                File.Copy(baselineFile, baselineOutPath, overwrite: true);
                File.Copy(currentFile, currentOutPath, overwrite: true);

                // Generate diff image
                GeneratePixelDiff(baselineFile, currentFile, diffOutPath, tolerance);

                diffPaths[frame.Name] = diffOutName;

                if (verbose)
                {
                    Console.WriteLine($"  ✓ {frame.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ✗ {frame.Name}: {ex.Message}");
            }
        }

        return diffPaths;
    }

    private static void GeneratePixelDiff(string baselinePath, string currentPath, string outputPath, int tolerance = 0)
    {
        using var baseline = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(baselinePath);
        using var current = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(currentPath);

        // Use the max dimensions
        var width = Math.Max(baseline.Width, current.Width);
        var height = Math.Max(baseline.Height, current.Height);

        using var diff = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baselinePixel = (x < baseline.Width && y < baseline.Height)
                    ? baseline[x, y]
                    : new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 0);

                var currentPixel = (x < current.Width && y < current.Height)
                    ? current[x, y]
                    : new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 0);

                // Compute absolute difference per channel
                var dr = Math.Abs(currentPixel.R - baselinePixel.R);
                var dg = Math.Abs(currentPixel.G - baselinePixel.G);
                var db = Math.Abs(currentPixel.B - baselinePixel.B);
                var da = Math.Abs(currentPixel.A - baselinePixel.A);

                // Check if any channel exceeds tolerance
                var maxDelta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));

                if (maxDelta > tolerance)
                {
                    // Highlight differences in magenta (visible on most backgrounds)
                    var intensity = (byte)Math.Min(255, (dr + dg + db + da) / 2 + 128);
                    diff[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32(intensity, 0, intensity, 255);
                }
                else
                {
                    // Unchanged: show dimmed grayscale
                    var gray = (byte)((currentPixel.R + currentPixel.G + currentPixel.B) / 6);
                    diff[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32(gray, gray, gray, 128);
                }
            }
        }

        using var outputStream = File.Create(outputPath);
        diff.Save(outputStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
    }

    private static string? ResolveScenePath(ComposeManifest manifest, string manifestPath)
    {
        // Resolve the scene path from the manifest output directory
        // The scene is generated as {DocumentName}View.tscn
        var outputDir = manifest.ResolveOutputPath(manifestPath);
        if (outputDir == null)
        {
            return null;
        }

        // Look for .tscn file in output directory
        if (Directory.Exists(outputDir))
        {
            var tscnFiles = Directory.GetFiles(outputDir, "*.tscn");
            if (tscnFiles.Length > 0)
            {
                return tscnFiles[0];
            }
        }

        return null;
    }

    private static string? ResolveSnapshotRunnerPath(FileInfo? explicitPath = null)
    {
        // Use explicit path if provided
        if (explicitPath != null && explicitPath.Exists)
        {
            return explicitPath.FullName;
        }

        // Look for SnapshotRunner.gd relative to the CLI executable
        var exeDir = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? ".";

        // Check various locations
        var candidates = new[]
        {
            Path.Combine(exeDir, "godot", "SnapshotRunner.gd"),
            Path.Combine(exeDir, "..", "..", "..", "..", "godot", "SnapshotRunner.gd"),
            Path.Combine(Environment.CurrentDirectory, "godot", "SnapshotRunner.gd"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static int InvokeGodotHeadless(
        string godotPath,
        string runnerPath,
        string scenePath,
        string statesPath,
        string outputPath,
        int timeout,
        bool verbose)
    {
        // Build command line arguments
        var args = new List<string>
        {
            "--headless",
            "--quit-after", timeout.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--script", runnerPath,
            "--",
            "--scene", scenePath,
            "--states", statesPath,
            "--out", outputPath
        };

        if (verbose)
        {
            args.Add("--verbose");
        }

        if (verbose)
        {
            Console.WriteLine($"Running: {godotPath} {string.Join(" ", args)}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = godotPath,
            Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                if (verbose)
                {
                    Console.WriteLine($"[Godot] {e.Data}");
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
                Console.Error.WriteLine($"[Godot:err] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exited = process.WaitForExit(timeout * 1000 + 5000); // Add 5s grace period

        if (!exited)
        {
            Console.Error.WriteLine("Error: Godot process timed out");
            try { process.Kill(); } catch { }
            return -1;
        }

        return process.ExitCode;
    }

    private static string? ResolveGodotExecutable(FileInfo? explicitPath)
    {
        if (explicitPath != null && explicitPath.Exists)
        {
            return explicitPath.FullName;
        }

        // Check common locations on Windows
        var candidates = new List<string>();

        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            candidates.Add(Path.Combine(dir, "godot.exe"));
            candidates.Add(Path.Combine(dir, "godot"));
            // Also check for versioned executables
            candidates.Add(Path.Combine(dir, "Godot_v4.5-stable_mono_win64_console.exe"));
            candidates.Add(Path.Combine(dir, "Godot_v4.5-stable_mono_win64.exe"));
        }

        // Check scoop
        var scoopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "scoop", "apps", "godot-mono", "current");
        if (Directory.Exists(scoopPath))
        {
            var exePattern = "Godot_v*_mono_win64_console.exe";
            var matches = Directory.GetFiles(scoopPath, exePattern);
            candidates.AddRange(matches);
        }

        // Check program files
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        candidates.Add(Path.Combine(programFiles, "Godot", "Godot.exe"));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? CaptureGodotVersion(string? godotPath)
    {
        if (string.IsNullOrEmpty(godotPath) || !File.Exists(godotPath))
        {
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = godotPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            // Godot outputs something like "4.5.stable.mono"
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static RunnerInfo CaptureRunnerInfo(bool dryRun)
    {
        // Detect CI environment
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_PIPELINES"));

        string? ciRunner = null;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
        {
            ciRunner = $"github-actions/{Environment.GetEnvironmentVariable("RUNNER_OS")}/{Environment.GetEnvironmentVariable("RUNNER_ARCH")}";
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_PIPELINES")))
        {
            ciRunner = "azure-pipelines";
        }

        return new RunnerInfo
        {
            Os = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}",
            DotnetVersion = Environment.Version.ToString(),
            IsCI = isCI,
            CIRunner = ciRunner,
            Headless = dryRun || isCI // Assume headless in CI or dry-run
        };
    }

    private static void CreatePlaceholderPng(string outputPath, int width, int height, int colorSeed)
    {
        // Create minimal valid PNG with solid color
        // For MVP, we create a small placeholder to prove pipeline works
        // Full implementation will use actual Godot rendering

        // Simple approach: write a placeholder text file for now
        // Real PNG generation will come from Godot viewport capture
        var placeholderContent = $"PLACEHOLDER: {width}x{height} seed={colorSeed}\n" +
                                 $"This file will be replaced with actual rendered PNG\n" +
                                 $"when Godot headless rendering is integrated.";
        File.WriteAllText(outputPath + ".placeholder.txt", placeholderContent);

        // Create a minimal 1-pixel PNG (valid format for tools to process)
        // PNG header + IHDR + IDAT + IEND
        // This is the smallest valid PNG possible
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR chunk length
            0x49, 0x48, 0x44, 0x52, // "IHDR"
            0x00, 0x00, 0x00, 0x01, // width = 1
            0x00, 0x00, 0x00, 0x01, // height = 1
            0x08, 0x02, // 8-bit RGB
            0x00, 0x00, 0x00, // compression, filter, interlace
            0x90, 0x77, 0x53, 0xDE, // IHDR CRC
            0x00, 0x00, 0x00, 0x0C, // IDAT chunk length
            0x49, 0x44, 0x41, 0x54, // "IDAT"
            0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00, // compressed pixel data
            0x02, 0xFE, 0x01, 0xFE, // data
            0x4C, 0xE8, 0x46, 0x18, // IDAT CRC
            0x00, 0x00, 0x00, 0x00, // IEND chunk length
            0x49, 0x45, 0x4E, 0x44, // "IEND"
            0xAE, 0x42, 0x60, 0x82  // IEND CRC
        };
        File.WriteAllBytes(outputPath, pngBytes);
    }

    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            sanitized.Append(invalid.Contains(c) ? '_' : c);
        }
        return sanitized.ToString();
    }
}
