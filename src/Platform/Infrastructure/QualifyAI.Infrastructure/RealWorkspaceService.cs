using System.Linq;

namespace QualifyAI.Infrastructure;

public sealed class RealWorkspaceService(AppDbContext db)
{
    public object Options()
    {
        _ = db;
        var registry = new Acquisition.AutonomousAcquisitionTemplateRegistry();
        var templates = registry.List();

        return new
        {
            useCases = templates.Select(t => new
            {
                id = t.UseCaseId,
                name = UseCaseName(t.UseCaseId),
                description = UseCaseDescription(t.UseCaseId)
            }).DistinctBy(x => x.id).ToArray(),
            templates = templates.Select(t => new
            {
                id = t.Code,
                name = t.Name,
                useCaseId = t.UseCaseId,
                description = $"{t.Industry} acquisition for {t.Region}.",
                requiredModules = new[] { "discovery", "enrichment", "qualification", "campaign-routing" }
            }).ToArray(),
            schedule = "daily"
        };
    }

    private static string UseCaseName(string id) => id switch
    {
        "fleet" => "Fleet & Mobility",
        "logistics" => "Logistics & Transport",
        "construction-materials" => "Construction Materials",
        "saas" => "SaaS",
        "software" => "Software",
        "custom" => "Custom ICP",
        _ => id
    };

    private static string UseCaseDescription(string id) => id switch
    {
        "fleet" => "Acquire fleet, mobility and vehicle-operation prospects.",
        "logistics" => "Acquire logistics, freight and transport prospects.",
        "construction-materials" => "Acquire construction-material distributors and building-supply prospects.",
        "saas" => "Acquire B2B SaaS and cloud-software prospects.",
        "software" => "Acquire enterprise and business-software prospects.",
        "custom" => "Configure a tenant-specific acquisition ICP.",
        _ => "Configure autonomous acquisition for this operating area."
    };
}
