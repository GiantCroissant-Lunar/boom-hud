using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl;
using BoomHud.Dsl.Figma;
using BoomHud.Gen.Avalonia;
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
            var generateCommand = new Command("generate", "Generate UI code from a Figma design JSON file");
            var inputArg = new Argument<FileInfo>("input", "Input Figma JSON file");
            var targetOption = new Option<string>("--target", () => "terminalGui", "Target backend (terminalGui, avalonia, all)");
            var outputOption = new Option<DirectoryInfo?>("--output", "Output directory for generated files");
            var namespaceOption = new Option<string>("--namespace", () => "Generated", "Namespace for generated code");
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

            generateCommand.AddArgument(inputArg);
            generateCommand.AddOption(targetOption);
            generateCommand.AddOption(outputOption);
            generateCommand.AddOption(namespaceOption);
            generateCommand.AddOption(nodeOption);
            generateCommand.AddOption(annotationsOption);
            generateCommand.AddOption(variablesOption);
            generateCommand.AddOption(themeNameOption);
            generateCommand.AddOption(themeCollectionOption);
            generateCommand.AddOption(themeModeOption);
            generateCommand.AddOption(descriptionReplaceOption);

            generateCommand.SetHandler((InvocationContext context) =>
            {
                var input = context.ParseResult.GetValueForArgument(inputArg);
                var target = context.ParseResult.GetValueForOption(targetOption) ?? "terminalGui";
                var output = context.ParseResult.GetValueForOption(outputOption);
                var @namespace = context.ParseResult.GetValueForOption(namespaceOption) ?? "Generated";
                var node = context.ParseResult.GetValueForOption(nodeOption);
                var annotations = context.ParseResult.GetValueForOption(annotationsOption);
                var variables = context.ParseResult.GetValueForOption(variablesOption);
                var themeName = context.ParseResult.GetValueForOption(themeNameOption);
                var themeCollection = context.ParseResult.GetValueForOption(themeCollectionOption);
                var themeMode = context.ParseResult.GetValueForOption(themeModeOption);
                var descriptionReplacements = context.ParseResult.GetValueForOption(descriptionReplaceOption);

                HandleGenerate(
                    input,
                    target,
                    output,
                    @namespace,
                    node,
                    annotations,
                    variables,
                    themeName,
                    themeCollection,
                    themeMode,
                    descriptionReplacements);
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

            rootCommand.AddCommand(generateCommand);
            rootCommand.AddCommand(validateCommand);
            rootCommand.AddCommand(initCommand);

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void HandleGenerate(
        FileInfo input,
        string target,
        DirectoryInfo? output,
        string @namespace,
        string? node,
        FileInfo? annotations,
        FileInfo? variables,
        string? themeName,
        string? themeCollection,
        string? themeMode,
        string[]? descriptionReplacements)
    {
        if (!input.Exists)
        {
            throw new FileNotFoundException($"File not found: {input.FullName}");
        }

        var outputRoot = output?.FullName ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputRoot);

        Console.WriteLine($"Loading Figma JSON from: {input.FullName}");
        var json = File.ReadAllText(input.FullName);

        var parser = new FigmaParser();
        HudDocument document = string.IsNullOrWhiteSpace(node)
            ? parser.Parse(json)
            : parser.ParseNode(json, node);

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
                throw new InvalidOperationException($"Generation failed for backend '{backend}'.");
            }

            var backendRoot = targets.Count > 1
                ? Path.Combine(outputRoot, backend)
                : outputRoot;

            WriteGeneratedFiles(backendRoot, result.Files);
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
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
            "all" => ["TerminalGui", "Avalonia"],
            _ => throw new ArgumentException($"Unknown target: {target}")
        };
    }

    private static IBackendGenerator CreateGenerator(string backend)
    {
        return backend switch
        {
            "TerminalGui" => new TerminalGuiGenerator(),
            "Avalonia" => new AvaloniaGenerator(),
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
}
