namespace QualifyAI.Identity.Application.Licensing;

public sealed record LicensePlanDefinition(string Code, string Name, int DefaultMaxUsers, IReadOnlyCollection<string> Modules);

public static class LicensePlanCatalog
{
    public static readonly IReadOnlyCollection<LicensePlanDefinition> Plans =
    [
        new("demo", "Demo", 5, ["crm", "inbox", "ticketing", "automation", "knowledge", "ai", "analytics", "integrations", "settings", "billing"]),
        new("starter", "Starter", 3, ["crm", "inbox", "ticketing", "settings", "billing"]),
        new("growth", "Growth", 10, ["crm", "inbox", "ticketing", "automation", "knowledge", "analytics", "integrations", "settings", "billing"]),
        new("business", "Business", 50, ["crm", "inbox", "ticketing", "automation", "knowledge", "ai", "analytics", "integrations", "settings", "billing"]),
        new("enterprise", "Enterprise", 500, ["crm", "inbox", "ticketing", "automation", "knowledge", "ai", "analytics", "integrations", "settings", "billing"])
    ];

    public static LicensePlanDefinition Get(string plan) => Plans.FirstOrDefault(x => x.Code.Equals(plan?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new IdentityValidationException("plan", $"Unknown license plan '{plan}'.");

    public static IReadOnlyCollection<string> ValidateModules(string plan, IEnumerable<string> requested)
    {
        var definition = Get(plan);
        var modules = requested.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).Distinct().ToArray();
        var invalid = modules.Except(definition.Modules, StringComparer.OrdinalIgnoreCase).ToArray();
        if (invalid.Length > 0) throw new IdentityValidationException("modules", $"Plan '{definition.Code}' does not include: {string.Join(", ", invalid)}.");
        if (!modules.Contains("settings") || !modules.Contains("billing"))
            throw new IdentityValidationException("modules", "Administration and billing modules are required so the tenant cannot lock itself out.");
        return modules;
    }
}
