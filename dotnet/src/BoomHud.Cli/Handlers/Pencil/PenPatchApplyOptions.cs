namespace BoomHud.Cli.Handlers.Pencil;

public sealed record PenPatchApplyOptions
{
    public FileInfo? PenFile { get; init; }

    public FileInfo? BatchOpsFile { get; init; }

    public FileInfo? PatchPlanFile { get; init; }

    public FileInfo? OutFile { get; init; }

    public bool PrintSummary { get; init; } = true;
}
