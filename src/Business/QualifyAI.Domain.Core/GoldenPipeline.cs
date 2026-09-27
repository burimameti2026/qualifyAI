namespace QualifyAI.Domain;

public static class GoldenPipeline
{
    public const string Name = "Golden Pipeline";
    public const string ModuleCode = "golden_pipeline";

    public static readonly GoldenPipelineStageDefinition[] DefaultStages =
    [
        new("New", 10, 0m, false, false),
        new("Qualifying", 20, 15m, false, false),
        new("Qualified", 30, 30m, false, false),
        new("Discovery", 40, 45m, false, false),
        new("Proposal", 50, 65m, false, false),
        new("Negotiation", 60, 80m, false, false),
        new("Won", 90, 100m, true, false),
        new("Lost", 100, 0m, false, true)
    ];
}

public sealed record GoldenPipelineStageDefinition(string Name, int SortOrder, decimal Probability, bool IsWon, bool IsLost);
