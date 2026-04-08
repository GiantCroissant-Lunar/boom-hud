using System;
using System.IO;
using System.Linq;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl;
using BoomHud.Dsl.Figma;
using BoomHud.Gen.Avalonia;
using BoomHud.Gen.TerminalGui;
using Path = System.IO.Path;

namespace BoomHud.Sample.Generation;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            // Move up from bin/{Configuration}/{TFM} to project root
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            var designFileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : "hud-dashboard.json";
            var designPath = Path.Combine(projectRoot, "design", designFileName);

            if (!File.Exists(designPath))
            {
                Console.Error.WriteLine($"Design file not found: {designPath}");
                return 1;
            }

            Console.WriteLine($"Loading Figma JSON from: {designPath}");
            var json = File.ReadAllText(designPath);

            var parser = new FigmaParser();
            var document = parser.Parse(json);

            var variablesFileName = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                ? args[1]
                : Path.GetFileNameWithoutExtension(designFileName) + ".variables.json";
            var variablesPath = Path.Combine(projectRoot, "design", variablesFileName);

            ThemeDocument? theme = null;
            if (File.Exists(variablesPath))
            {
                Console.WriteLine($"Loading Figma variables from: {variablesPath}");
                var variablesJson = File.ReadAllText(variablesPath);
                try
                {
                    theme = FigmaThemeParser.Parse(
                        variablesJson,
                        themeName: Path.GetFileNameWithoutExtension(variablesFileName));
                    Console.WriteLine(
                        $"Parsed theme '{theme.Name}': " +
                        $"{theme.Colors.Count} colors, " +
                        $"{theme.Dimensions.Count} dimensions, " +
                        $"{theme.FontSizes.Count} font sizes.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: failed to parse Figma variables: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"No Figma variables file found at: {variablesPath} (skipping theme parsing)");
            }

            var options = new GenerationOptions
            {
                Namespace = "BoomHud.Sample.Generated",
                IncludeComments = true,
                UseNullableAnnotations = true,
                OutputDirectory = ".",
                Theme = theme
            };

            var outputRoot = Path.Combine(projectRoot, "Generated");
            Directory.CreateDirectory(outputRoot);

            Generate("TerminalGui", new TerminalGuiGenerator(), document, options, outputRoot);
            Generate("Avalonia", new AvaloniaGenerator(), document, options, outputRoot);

            Console.WriteLine();
            Console.WriteLine("Done. Inspect the 'Generated' folder next to this executable.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during generation: {ex}");
            return 1;
        }
    }

    private static void Generate(string backendName, IBackendGenerator generator, HudDocument document, GenerationOptions options, string outputRoot)
    {
        Console.WriteLine();
        Console.WriteLine($"=== Generating {backendName} code ===");

        var result = generator.Generate(document, options);

        var backendRoot = Path.Combine(outputRoot, backendName);
        foreach (var file in result.Files)
        {
            var targetPath = Path.Combine(backendRoot, file.Path);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(targetPath, file.Content);
            Console.WriteLine($"Wrote {backendName} file: {targetPath}");
        }
    }

}
