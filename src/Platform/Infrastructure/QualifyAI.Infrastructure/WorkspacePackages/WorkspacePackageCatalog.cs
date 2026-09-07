namespace QualifyAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageDefinition(
    string Id,
    string Name,
    string Version,
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> Included);

public static class WorkspacePackageCatalog
{
    public static readonly WorkspacePackageDefinition FusionFleetPromotion = new(
        "fusionfleet-promotion",
        "FusionFleet Promotion",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "FusionFleet ICP", "Prospecting agent", "Daily discovery workflow", "Target list", "Promotion campaign" });

    public static readonly WorkspacePackageDefinition QualifyAiAcquisition = new(
        "qualifyai-acquisition",
        "QualifyAI Acquisition",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "Revenue ICP", "Qualification agent", "Revenue workflow", "Target list", "Pilot campaign" });

    public static readonly WorkspacePackageDefinition Blank = new(
        "blank", "Blank Workspace", "1.0", Array.Empty<string>(), Array.Empty<string>());

    public static bool TryGet(string id, out WorkspacePackageDefinition definition)
    {
        definition = id.ToLowerInvariant() switch
        {
            "fusionfleet-promotion" => FusionFleetPromotion,
            "qualifyai-acquisition" => QualifyAiAcquisition,
            "blank" => Blank,
            _ => null!
        };
        return definition is not null;
    }
}
