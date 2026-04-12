using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Rules;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesProofSubtreeCommand
{
    public static Command Build()
    {
        var command = new Command("proof-subtree", "Evaluate the four uGUI subtree-search preconditions against measured layout artifacts");

        var visualIrOption = new Option<FileInfo?>("--visual-ir", "Visual IR artifact emitted from generation")
        {
            IsRequired = true
        };
        var subtreeStableIdOption = new Option<string>("--subtree-stable-id", "Stable id of the subtree to evaluate")
        {
            IsRequired = true
        };
        var actualLayoutOption = new Option<FileInfo[]>("--actual-layout", "One or more Unity actual-layout snapshots emitted beside captured images")
        {
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };
        var labelOption = new Option<string[]>("--label", "Optional label per actual-layout input")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var candidateKeyOption = new Option<string[]>("--candidate-key", "Optional candidate key per actual-layout input. Repeated keys mark repeated captures of the same candidate for determinism checks.")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var buildProgramOption = new Option<FileInfo?>("--build-program", "Optional replayable uGUI build-program artifact with candidate catalogs and accepted selections");
        var referenceImageOption = new Option<FileInfo?>("--reference-image", "Optional Pen reference image used for normalized root/subtree image scoring");
        var outOption = new Option<FileInfo?>("--out", "Output report file");
        var positionToleranceOption = new Option<double>("--position-tolerance", () => 0.01, "Maximum allowed determinism drift for X/Y values");
        var sizeToleranceOption = new Option<double>("--size-tolerance", () => 0.01, "Maximum allowed determinism drift for width/height values");
        var fontToleranceOption = new Option<double>("--font-tolerance", () => 0.01, "Maximum allowed determinism drift for font-size values");
        var imageToleranceOption = new Option<int>("--image-tolerance", () => 8, "Per-channel pixel delta tolerance for image scoring");
        var imageNormalizeModeOption = new Option<string>("--image-normalize", () => "stretch", "Image normalization mode: off, stretch, or cover");
        var summaryOption = new Option<bool>("--summary", () => true, "Print a concise proof summary");

        command.AddOption(visualIrOption);
        command.AddOption(subtreeStableIdOption);
        command.AddOption(actualLayoutOption);
        command.AddOption(labelOption);
        command.AddOption(candidateKeyOption);
        command.AddOption(buildProgramOption);
        command.AddOption(referenceImageOption);
        command.AddOption(outOption);
        command.AddOption(positionToleranceOption);
        command.AddOption(sizeToleranceOption);
        command.AddOption(fontToleranceOption);
        command.AddOption(imageToleranceOption);
        command.AddOption(imageNormalizeModeOption);
        command.AddOption(summaryOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new UGuiSubtreeProofOptions
            {
                VisualIrFile = context.ParseResult.GetValueForOption(visualIrOption),
                SubtreeStableId = context.ParseResult.GetValueForOption(subtreeStableIdOption) ?? string.Empty,
                ActualLayoutFiles = context.ParseResult.GetValueForOption(actualLayoutOption) ?? [],
                Labels = context.ParseResult.GetValueForOption(labelOption) ?? [],
                CandidateKeys = context.ParseResult.GetValueForOption(candidateKeyOption) ?? [],
                BuildProgramFile = context.ParseResult.GetValueForOption(buildProgramOption),
                ReferenceImageFile = context.ParseResult.GetValueForOption(referenceImageOption),
                OutFile = context.ParseResult.GetValueForOption(outOption),
                PositionTolerance = context.ParseResult.GetValueForOption(positionToleranceOption),
                SizeTolerance = context.ParseResult.GetValueForOption(sizeToleranceOption),
                FontTolerance = context.ParseResult.GetValueForOption(fontToleranceOption),
                ImageTolerance = context.ParseResult.GetValueForOption(imageToleranceOption),
                ImageNormalizeMode = context.ParseResult.GetValueForOption(imageNormalizeModeOption) ?? "stretch",
                PrintSummary = context.ParseResult.GetValueForOption(summaryOption)
            };

            context.ExitCode = UGuiSubtreeProofHandler.Execute(options);
        });

        return command;
    }
}
