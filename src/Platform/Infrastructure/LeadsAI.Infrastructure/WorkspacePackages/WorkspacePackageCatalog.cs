namespace LeadsAI.Infrastructure.WorkspacePackages;

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
        "FusionFleet Logistics Growth",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "Logistics ICP", "AI prospecting agent", "Daily discovery workflow", "Target list", "Promotion campaign", "Automated follow-up" });

    public static readonly WorkspacePackageDefinition QualifyAiAcquisition = new(
        "leadsai-acquisition",
        "LeadsAI Acquisition",
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
            "leadsai-acquisition" => QualifyAiAcquisition,
            "blank" => Blank,
            _ => null!
        };
        return definition is not null;
    }
}
