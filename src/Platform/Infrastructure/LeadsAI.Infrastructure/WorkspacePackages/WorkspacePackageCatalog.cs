namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageDefinition(
    string Id,
    string Name,
    string Version,
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> Included,
    string Category = "industry",
    string ProvisioningMode = "profile");

public static class WorkspacePackageCatalog
{
    public static readonly WorkspacePackageDefinition Manufacturing = new(
        "manufacturing", "Manufacturing & Production", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Production", "BOM & materials", "Quality", "Maintenance", "Suppliers", "Production planning" });

    public static readonly WorkspacePackageDefinition Logistics = new(
        "logistics", "Logistics & Transportation", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Shipments", "Routes", "Fleet", "Drivers", "Dispatch", "Carrier management" });

    public static readonly WorkspacePackageDefinition Warehouse = new(
        "warehouse", "Warehouse Management", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Receiving", "Put-away", "Inventory", "Picking", "Packing", "Cycle counts" });

    public static readonly WorkspacePackageDefinition Distribution = new(
        "distribution", "Distribution & Wholesale", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Orders", "Inventory allocation", "Replenishment", "Pricing", "Suppliers", "Dispatch" });

    public static readonly WorkspacePackageDefinition Delivery = new(
        "delivery", "Delivery & Last Mile", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Delivery orders", "Drivers", "Routes", "Stops", "Proof of delivery", "Returns" });

    public static readonly WorkspacePackageDefinition ThreePl = new(
        "3pl", "3PL & Contract Logistics", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Multi-client operations", "Warehouse", "Inventory", "SLAs", "Billing", "Carrier management" });

    public static readonly WorkspacePackageDefinition Retail = new(
        "retail-ecommerce", "Retail & E-commerce", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Products", "Orders", "Inventory", "Fulfillment", "Customers", "Returns" });

    public static readonly WorkspacePackageDefinition Construction = new(
        "construction-field-service", "Construction & Field Service", "1.0",
        new[] { "crm", "golden_pipeline" },
        new[] { "Projects", "Work orders", "Materials", "Technicians", "Scheduling", "Service SLAs" });

    public static readonly WorkspacePackageDefinition FusionFleetPromotion = new(
        "fusionfleet-promotion",
        "FusionFleet Logistics Growth",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "Logistics ICP", "AI prospecting agent", "Daily discovery workflow", "Target list", "Promotion campaign", "Automated follow-up" },
        "customer-scenario", "workspace");

    public static readonly WorkspacePackageDefinition QualifyAiAcquisition = new(
        "leadsai-acquisition",
        "LeadsAI Acquisition",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "Revenue ICP", "Qualification agent", "Revenue workflow", "Target list", "Pilot campaign" },
        "customer-scenario", "workspace");

    public static readonly WorkspacePackageDefinition Blank = new(
        "blank", "Blank Workspace", "1.0", Array.Empty<string>(), Array.Empty<string>(), "system", "workspace");

    public static IReadOnlyCollection<WorkspacePackageDefinition> All { get; } =
    [
        Manufacturing, Logistics, Warehouse, Distribution, Delivery, ThreePl, Retail, Construction,
        FusionFleetPromotion, QualifyAiAcquisition, Blank
    ];

    public static bool TryGet(string? id, out WorkspacePackageDefinition definition)
    {
        definition = All.FirstOrDefault(x => x.Id.Equals(id?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }
}
