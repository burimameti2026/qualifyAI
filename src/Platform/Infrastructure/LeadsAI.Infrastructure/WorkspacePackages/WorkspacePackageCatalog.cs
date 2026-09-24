namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageDefinition(
    string Id,
    string Name,
    string Version,
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> Included,
    IReadOnlyList<string>? Capabilities = null,
    IReadOnlyList<string>? Workflows = null,
    IReadOnlyList<string>? Agents = null,
    string Category = "industry",
    string ProvisioningMode = "operational");

public static class WorkspacePackageCatalog
{
    public static readonly WorkspacePackageDefinition Manufacturing = new(
        "manufacturing", "Manufacturing & Production", "1.0",
        new[] { "crm", "golden_pipeline", "production", "bom", "quality", "maintenance", "suppliers" },
        new[] { "Production", "BOM & materials", "Quality", "Maintenance", "Suppliers", "Production planning" },
        new[] { "CRM", "Golden Pipeline", "Production Operations", "Supplier Management" },
        new[] { "Lead qualification", "Supplier follow-up", "Customer acquisition" },
        new[] { "Qualification Agent", "Sales Agent" });

    public static readonly WorkspacePackageDefinition Logistics = new(
        "logistics", "Logistics & Transportation", "1.0",
        new[] { "crm", "golden_pipeline", "shipments", "routes", "fleet", "drivers", "dispatch" },
        new[] { "Shipments", "Routes", "Fleet", "Drivers", "Dispatch", "Carrier management" },
        new[] { "CRM", "Golden Pipeline", "Fleet Operations", "Dispatch" },
        new[] { "Lead qualification", "Carrier acquisition", "Customer follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition Warehouse = new(
        "warehouse", "Warehouse Management", "1.0",
        new[] { "crm", "golden_pipeline", "receiving", "putaway", "inventory", "picking", "packing", "cycle_counts" },
        new[] { "Receiving", "Put-away", "Inventory", "Picking", "Packing", "Cycle counts" },
        new[] { "CRM", "Golden Pipeline", "Inventory Operations", "Supplier Management" },
        new[] { "Supplier acquisition", "Account qualification", "Customer follow-up" },
        new[] { "Qualification Agent" });

    public static readonly WorkspacePackageDefinition Distribution = new(
        "distribution", "Distribution & Wholesale", "1.0",
        new[] { "crm", "golden_pipeline", "orders", "inventory_allocation", "replenishment", "pricing", "suppliers", "dispatch" },
        new[] { "Orders", "Inventory allocation", "Replenishment", "Pricing", "Suppliers", "Dispatch" },
        new[] { "CRM", "Golden Pipeline", "Order Management", "Supplier Management" },
        new[] { "Account qualification", "Supplier acquisition", "Customer follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition Delivery = new(
        "delivery", "Delivery & Last Mile", "1.0",
        new[] { "crm", "golden_pipeline", "delivery_orders", "drivers", "routes", "stops", "proof_of_delivery", "returns" },
        new[] { "Delivery orders", "Drivers", "Routes", "Stops", "Proof of delivery", "Returns" },
        new[] { "CRM", "Golden Pipeline", "Delivery Operations", "Customer Management" },
        new[] { "Customer acquisition", "Account qualification", "Follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition ThreePl = new(
        "3pl", "3PL & Contract Logistics", "1.0",
        new[] { "crm", "golden_pipeline", "multi_client", "warehouse", "inventory", "slas", "billing", "carrier_management" },
        new[] { "Multi-client operations", "Warehouse", "Inventory", "SLAs", "Billing", "Carrier management" },
        new[] { "CRM", "Golden Pipeline", "Multi-client Operations", "Carrier Management" },
        new[] { "Customer acquisition", "Account qualification", "Customer follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition Retail = new(
        "retail-ecommerce", "Retail & E-commerce", "1.0",
        new[] { "crm", "golden_pipeline", "products", "orders", "inventory", "fulfillment", "customers", "returns" },
        new[] { "Products", "Orders", "Inventory", "Fulfillment", "Customers", "Returns" },
        new[] { "CRM", "Golden Pipeline", "Customer Management", "Fulfillment" },
        new[] { "Partner acquisition", "Account qualification", "Customer follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition Construction = new(
        "construction-field-service", "Construction & Field Service", "1.0",
        new[] { "crm", "golden_pipeline", "projects", "work_orders", "materials", "technicians", "scheduling", "service_slas" },
        new[] { "Projects", "Work orders", "Materials", "Technicians", "Scheduling", "Service SLAs" },
        new[] { "CRM", "Golden Pipeline", "Service Operations", "Customer Management" },
        new[] { "Account acquisition", "Lead qualification", "Service follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition FoodBeverage = new(
        "food-beverage", "Food & Beverage", "1.0",
        new[] { "crm", "golden_pipeline", "products", "orders", "inventory", "production", "quality", "suppliers", "traceability" },
        new[] { "Products", "Production", "Inventory", "Quality", "Suppliers", "Traceability", "Orders" },
        new[] { "CRM", "Golden Pipeline", "Production Operations", "Traceability", "Supplier Management" },
        new[] { "Account qualification", "Supplier acquisition", "Customer follow-up" },
        new[] { "Acquisition Agent", "Qualification Agent" });

    public static readonly WorkspacePackageDefinition FusionFleetPromotion = new(
        "fusionfleet-promotion",
        "FusionFleet Logistics Growth",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "Logistics ICP", "AI prospecting agent", "Daily discovery workflow", "Target list", "Promotion campaign", "Automated follow-up" },
        new[] { "CRM", "Acquisition", "Campaigns" },
        new[] { "Daily discovery", "Enrichment", "Qualification", "Promotion" },
        new[] { "Acquisition Agent", "Qualification Agent" },
        "customer-scenario", "workspace");

    public static readonly WorkspacePackageDefinition QualifyAiAcquisition = new(
        "leadsai-acquisition",
        "LeadsAI Acquisition",
        "1.0",
        new[] { "crm", "ai_agents", "automations" },
        new[] { "Revenue ICP", "Qualification agent", "Revenue workflow", "Target list", "Pilot campaign" },
        new[] { "CRM", "Acquisition", "Campaigns" },
        new[] { "Discovery", "Enrichment", "Qualification", "Campaign approval" },
        new[] { "Acquisition Agent", "Qualification Agent" },
        "customer-scenario", "workspace");

    public static readonly WorkspacePackageDefinition Blank = new(
        "blank", "Blank Workspace", "1.0", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "system", "workspace");

    public static IReadOnlyCollection<WorkspacePackageDefinition> All { get; } =
    [
        Manufacturing, Logistics, Warehouse, Distribution, Delivery, ThreePl, Retail, Construction, FoodBeverage,
        FusionFleetPromotion, QualifyAiAcquisition, Blank
    ];

    public static bool TryGet(string? id, out WorkspacePackageDefinition definition)
    {
        definition = All.FirstOrDefault(x => x.Id.Equals(id?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }
}
