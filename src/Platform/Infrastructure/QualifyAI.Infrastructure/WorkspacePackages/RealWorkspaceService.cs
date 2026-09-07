using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.WorkspacePackages;

public sealed record RealWorkspaceUseCase(string Id, string Name, string Description);
public sealed record RealWorkspaceTemplate(string Id, string Name, string UseCaseId, string Description, IReadOnlyList<string> RequiredModules, IReadOnlyList<RealWorkspaceProspect> Prospects);
public sealed record RealWorkspaceProspect(string Id, string CompanyName, string Domain, string ContactName, string Email, string JobTitle, string Industry, string Country, string PainHypothesis, string Offer);
public sealed record RealWorkspaceDraft(Guid WorkspaceId, string Status, string UseCaseId, string TemplateId, string Name, IReadOnlyList<RealWorkspaceProspect> Prospects, IReadOnlyList<string> SelectedProspectIds, DateTime UpdatedAtUtc, DateTime? ActivatedAtUtc);
public sealed record RealWorkspaceOptions(IReadOnlyList<RealWorkspaceUseCase> UseCases, IReadOnlyList<RealWorkspaceTemplate> Templates);
public sealed record PrepareRealWorkspaceRequest(string UseCaseId, string TemplateId, string? Name);
public sealed record SaveRealWorkspaceRequest(Guid WorkspaceId, string? Name, IReadOnlyList<RealWorkspaceProspect> Prospects, IReadOnlyList<string> SelectedProspectIds);

public sealed class RealWorkspaceService(AppDbContext db)
{
    private const string SettingKey = "real-workspace.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RealWorkspaceOptions Options()
    {
        var templates = Templates();
        return new(
            new[]
            {
                new RealWorkspaceUseCase("logistics", "Logistics", "Fleet, dispatch, delivery and 3PL acquisition."),
                new RealWorkspaceUseCase("sales", "Sales", "B2B revenue acquisition and qualification."),
                new RealWorkspaceUseCase("marketing", "Promotion / Marketing", "Promotion, demand generation and campaign operations."),
                new RealWorkspaceUseCase("saas", "SaaS", "Software company prospecting and pipeline growth."),
                new RealWorkspaceUseCase("ecommerce", "E-commerce", "E-commerce growth and commercial prospecting.")
            },
            templates);
    }

    public async Task<RealWorkspaceDraft?> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        var setting = await db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == SettingKey, ct);
        return setting is null ? null : Deserialize(setting.Value);
    }

    public async Task<RealWorkspaceDraft> PrepareAsync(Guid tenantId, PrepareRealWorkspaceRequest request, CancellationToken ct = default)
    {
        var template = Templates().FirstOrDefault(x => x.Id.Equals(request.TemplateId, StringComparison.OrdinalIgnoreCase) && x.UseCaseId.Equals(request.UseCaseId, StringComparison.OrdinalIgnoreCase));
        if (template is null) throw new InvalidOperationException("The selected workspace template is not available for this use case.");

        var existing = await db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == SettingKey, ct);
        var workspace = new RealWorkspaceDraft(Guid.NewGuid(), "draft", template.UseCaseId, template.Id, string.IsNullOrWhiteSpace(request.Name) ? template.Name : request.Name.Trim(), template.Prospects, Array.Empty<string>(), DateTime.UtcNow, null);
        var json = JsonSerializer.Serialize(workspace, JsonOptions);
        if (existing is null) db.TenantSettings.Add(new TenantSetting { TenantId = tenantId, Key = SettingKey, Value = json });
        else { existing.Value = json; existing.UpdatedAtUtc = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        return workspace;
    }

    public async Task<RealWorkspaceDraft> SaveAsync(Guid tenantId, SaveRealWorkspaceRequest request, CancellationToken ct = default)
    {
        var current = await GetAsync(tenantId, ct) ?? throw new InvalidOperationException("Prepare a workspace before saving it.");
        if (current.WorkspaceId != request.WorkspaceId) throw new InvalidOperationException("The workspace draft is no longer current.");
        if (current.Status == "active") throw new InvalidOperationException("An active workspace cannot be edited from the preparation flow.");

        var allowed = request.Prospects.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = request.SelectedProspectIds.Where(allowed.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var workspace = current with
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? current.Name : request.Name.Trim(),
            Prospects = request.Prospects,
            SelectedProspectIds = selected,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await PersistAsync(tenantId, workspace, ct);
        return workspace;
    }

    public async Task<RealWorkspaceDraft> ActivateAsync(Guid tenantId, Guid workspaceId, CancellationToken ct = default)
    {
        var current = await GetAsync(tenantId, ct) ?? throw new InvalidOperationException("Prepare a workspace before activating it.");
        if (current.WorkspaceId != workspaceId) throw new InvalidOperationException("The workspace draft is no longer current.");
        if (current.Status == "active") return current;
        if (current.SelectedProspectIds.Count == 0) throw new InvalidOperationException("Select at least one prospect before activating the workspace.");

        var selected = current.Prospects.Where(x => current.SelectedProspectIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        var source = $"real-workspace:{current.WorkspaceId}";
        var existing = await db.Prospects.Where(x => x.TenantId == tenantId && x.DatasetOrigin == source).ToListAsync(ct);
        if (existing.Count == 0)
        {
            db.Prospects.AddRange(selected.Select(x => new Prospect
            {
                TenantId = tenantId,
                CompanyName = x.CompanyName,
                Domain = x.Domain,
                ContactName = x.ContactName,
                Email = x.Email,
                JobTitle = x.JobTitle,
                Industry = x.Industry,
                Country = x.Country,
                Source = "real-workspace",
                DatasetOrigin = source,
                PainHypothesis = x.PainHypothesis,
                Offer = x.Offer,
                VerificationStatus = "workspace-prepared"
            }));
            await db.SaveChangesAsync(ct);
        }

        var workspace = current with { Status = "active", UpdatedAtUtc = DateTime.UtcNow, ActivatedAtUtc = DateTime.UtcNow };
        await PersistAsync(tenantId, workspace, ct);
        return workspace;
    }

    private async Task PersistAsync(Guid tenantId, RealWorkspaceDraft workspace, CancellationToken ct)
    {
        var setting = await db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == SettingKey, ct);
        if (setting is null) db.TenantSettings.Add(new TenantSetting { TenantId = tenantId, Key = SettingKey, Value = JsonSerializer.Serialize(workspace, JsonOptions) });
        else { setting.Value = JsonSerializer.Serialize(workspace, JsonOptions); setting.UpdatedAtUtc = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
    }

    private static RealWorkspaceDraft Deserialize(string value) => JsonSerializer.Deserialize<RealWorkspaceDraft>(value, JsonOptions) ?? throw new InvalidOperationException("The saved workspace draft is invalid.");

    private static IReadOnlyList<RealWorkspaceTemplate> Templates() => new[]
    {
        new RealWorkspaceTemplate("fusionfleet-promotion", "FusionFleet Promotion", "logistics", "Logistics acquisition starter workspace.", new[] { "crm", "ai_agents", "automations" }, new[]
        {
            new RealWorkspaceProspect("ff-1", "Northstar Freight", "northstarfreight.example", "Elena Markovic", "elena@northstarfreight.example", "VP Operations", "Logistics", "North Macedonia", "Reduce dispatch exceptions and manual carrier coordination.", "Fleet and dispatch automation"),
            new RealWorkspaceProspect("ff-2", "Adria 3PL", "adria3pl.example", "Marko Petrov", "marko@adria3pl.example", "Operations Director", "3PL", "Serbia", "Improve shipment visibility and delivery exception handling.", "3PL acquisition workflow"),
            new RealWorkspaceProspect("ff-3", "Balkan Route Logistics", "balkanroute.example", "Ana Kovac", "ana@balkanroute.example", "Head of Fleet", "Logistics", "Croatia", "Lower empty miles and improve fleet utilization.", "Fleet intelligence"),
            new RealWorkspaceProspect("ff-4", "Danube Distribution", "danubedistribution.example", "Nikola Ilic", "nikola@danubedistribution.example", "COO", "Distribution", "Hungary", "Automate delivery follow-up and operational reporting.", "Delivery operations automation"),
            new RealWorkspaceProspect("ff-5", "EuroHaul Services", "eurohaul.example", "Sara Weiss", "sara@eurohaul.example", "Commercial Director", "Transport", "Austria", "Create a predictable acquisition motion for fleet operators.", "Autonomous prospecting")
        }),
        new RealWorkspaceTemplate("sales-acquisition", "B2B Sales Acquisition", "sales", "Revenue acquisition starter workspace.", new[] { "crm", "ai_agents", "automations" }, new[]
        {
            new RealWorkspaceProspect("sales-1", "Vertex Systems", "vertexsystems.example", "Daniel Stone", "daniel@vertexsystems.example", "CRO", "Software", "Germany", "Increase qualified pipeline without adding SDR headcount.", "AI-assisted sales development"),
            new RealWorkspaceProspect("sales-2", "Nova Industrial", "novaindustrial.example", "Mia Keller", "mia@novaindustrial.example", "Sales Director", "Industrial", "Italy", "Improve lead qualification and response speed.", "Revenue workflow automation"),
            new RealWorkspaceProspect("sales-3", "Orion Consulting", "orionconsulting.example", "Luka Marin", "luka@orionconsulting.example", "Managing Partner", "Consulting", "Slovenia", "Build a repeatable outbound acquisition process.", "Consulting growth"),
            new RealWorkspaceProspect("sales-4", "Apex Cloud", "apexcloud.example", "Sophie Hart", "sophie@apexcloud.example", "VP Sales", "SaaS", "Netherlands", "Prioritize high-fit accounts automatically.", "ICP-based acquisition"),
            new RealWorkspaceProspect("sales-5", "Mercury Tech", "mercurytech.example", "Ivan Ristic", "ivan@mercurytech.example", "Head of Revenue", "Technology", "North Macedonia", "Turn prospect signals into qualified opportunities.", "Signal-driven sales")
        }),
        new RealWorkspaceTemplate("promotion-growth", "Promotion Growth", "marketing", "Promotion and demand generation starter workspace.", new[] { "crm", "ai_agents", "automations" }, new[]
        {
            new RealWorkspaceProspect("mkt-1", "BrightMarket", "brightmarket.example", "Laura Chen", "laura@brightmarket.example", "VP Marketing", "Marketing", "France", "Improve campaign-to-pipeline conversion.", "AI campaign optimization"),
            new RealWorkspaceProspect("mkt-2", "LaunchWorks", "launchworks.example", "Tom Reed", "tom@launchworks.example", "Growth Lead", "Marketing", "United Kingdom", "Scale personalized promotion across target accounts.", "Personalized promotion"),
            new RealWorkspaceProspect("mkt-3", "SignalHouse", "signalhouse.example", "Maja Novak", "maja@signalhouse.example", "Demand Gen Director", "Technology", "Poland", "Convert intent signals into timely outreach.", "Intent-based promotion"),
            new RealWorkspaceProspect("mkt-4", "MarketPilot", "marketpilot.example", "David Young", "david@marketpilot.example", "CMO", "Software", "United States", "Connect promotion activity to revenue outcomes.", "Revenue marketing"),
            new RealWorkspaceProspect("mkt-5", "GrowthForge", "growthforge.example", "Sara Klein", "sara@growthforge.example", "Head of Growth", "E-commerce", "Austria", "Automate account discovery and campaign preparation.", "Growth automation")
        }),
        new RealWorkspaceTemplate("saas-acquisition", "SaaS Acquisition", "saas", "Software-company acquisition starter workspace.", new[] { "crm", "ai_agents", "automations" }, new[]
        {
            new RealWorkspaceProspect("saas-1", "CloudPeak", "cloudpeak.example", "Alex Turner", "alex@cloudpeak.example", "CEO", "SaaS", "Ireland", "Build a stronger qualified pipeline.", "Autonomous SaaS acquisition"),
            new RealWorkspaceProspect("saas-2", "DataMesh", "datamesh.example", "Nina Rossi", "nina@datamesh.example", "VP Revenue", "SaaS", "Italy", "Prioritize enterprise accounts by fit.", "Enterprise ICP"),
            new RealWorkspaceProspect("saas-3", "FlowOps", "flowops.example", "Jonas Berg", "jonas@flowops.example", "Head of Sales", "SaaS", "Sweden", "Reduce manual prospect research.", "Research automation"),
            new RealWorkspaceProspect("saas-4", "SecureStack", "securestack.example", "Eva Braun", "eva@securestack.example", "CRO", "Cybersecurity", "Germany", "Improve account scoring and qualification.", "AI qualification"),
            new RealWorkspaceProspect("saas-5", "RetailCloud", "retailcloud.example", "Omar Haddad", "omar@retailcloud.example", "Commercial Director", "SaaS", "Spain", "Turn buying signals into meetings.", "Signal-to-meeting")
        }),
        new RealWorkspaceTemplate("ecommerce-growth", "E-commerce Growth", "ecommerce", "E-commerce growth starter workspace.", new[] { "crm", "ai_agents", "automations" }, new[]
        {
            new RealWorkspaceProspect("ecom-1", "UrbanCart", "urbancart.example", "Emma Wilson", "emma@urbancart.example", "Head of Growth", "E-commerce", "United Kingdom", "Improve acquisition efficiency.", "Growth automation"),
            new RealWorkspaceProspect("ecom-2", "HomeNest", "homenest.example", "Stefan Bauer", "stefan@homenest.example", "COO", "E-commerce", "Germany", "Increase repeatable B2B partnerships.", "Partner acquisition"),
            new RealWorkspaceProspect("ecom-3", "ModaLane", "modalane.example", "Clara Rossi", "clara@modalane.example", "Commercial Director", "Retail", "Italy", "Find high-fit commercial partners.", "Partner prospecting"),
            new RealWorkspaceProspect("ecom-4", "GreenBasket", "greenbasket.example", "Milan Horvat", "milan@greenbasket.example", "Growth Director", "E-commerce", "Croatia", "Automate account discovery and outreach.", "Autonomous growth"),
            new RealWorkspaceProspect("ecom-5", "ShopSphere", "shopsphere.example", "Lea Fischer", "lea@shopsphere.example", "VP Sales", "E-commerce", "Austria", "Qualify new commercial opportunities faster.", "Opportunity qualification")
        })
    };
}
