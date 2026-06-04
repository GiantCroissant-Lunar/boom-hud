namespace BoomHud.Cli.Handlers.Pencil;

public sealed record PencilRefinementLoopOptions
{
    public FileInfo? PenFile { get; init; }

    public FileInfo? ReferenceFile { get; init; }

    public DirectoryInfo? WorkingDirectory { get; init; }

    public int MaxIterations { get; init; } = 5;

    public string NormalizeMode { get; init; } = "stretch";

    public int Tolerance { get; init; } = 8;

    public FileInfo? FontPath { get; init; }

    public bool PrintSummary { get; init; } = true;
}
