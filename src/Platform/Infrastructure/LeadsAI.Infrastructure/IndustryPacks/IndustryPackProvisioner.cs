using System.Text.Json;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Infrastructure.Acquisition;

namespace LeadsAI.Infrastructure.IndustryPacks;

public sealed record CampaignReadyDefinition(
    Guid IndustryPackId,
    string IndustryCode,
    string IndustryName,
    string Industry,
    string Region,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Keywords,
    int MinimumScore,
    string CampaignName,
    string Objective,
    string Goal,
    string SenderName,
    string SenderEmail,
    IReadOnlyList<CampaignReadyStep> Steps);

public sealed record CampaignReadyStep(
    int StepNumber,
    int DelayHours,
    string Channel,
    string SubjectTemplate,
    string BodyTemplate);

public sealed record IndustryPackProvisioningResult(
    Guid IndustryPackId,
    string IndustryCode,
    Guid TargetListId,
    Guid CampaignId,
    string CampaignStatus,
    string ProvisioningMode,
    CampaignReadyDefinition Definition);

public interface IIndustryPackProvisioner
{
    Task<IndustryPackProvisioningResult> ProvisionAsync(Guid tenantId, Guid industryPackId, CancellationToken ct = default, string? scenarioCode = null);
}

public sealed class IndustryPackProvisioner(
    AppDbContext db,
    IAutonomousAcquisitionTemplateRegistry templates,
    IAutonomousAcquisitionWorkflowPlanner planner) : IIndustryPackProvisioner
{
    private const string Version = "industry-pack.v1";

    public async Task<IndustryPackProvisioningResult> ProvisionAsync(Guid tenantId, Guid industryPackId, CancellationToken ct = default, string? scenarioCode = null)
    {
        // SQL Server uses a retrying execution strategy. The entire transaction must
        // execute inside that strategy so a transient failure can safely retry the
        // complete provisioning unit.
        var pack = await db.IndustryPacks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == industryPackId, ct)
            ?? throw new InvalidOperationException($"Industry pack '{industryPackId}' was not found.");

        var definition = BuildDefinition(pack, scenarioCode);
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // A retry re-enters this delegate. Clear state from a previous failed
            // attempt so Added/Modified entities are not replayed accidentally.
            db.ChangeTracker.Clear();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var installed = await db.TenantIndustryPacks.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.IndustryPackId == industryPackId, ct);

        if (installed is null)
        {
            db.TenantIndustryPacks.Add(new TenantIndustryPack
            {
                TenantId = tenantId,
                IndustryPackId = industryPackId,
                Enabled = true
            });
        }
        else
        {
            installed.Enabled = true;
            installed.UpdatedAtUtc = DateTime.UtcNow;
        }

        var marker = $"industry-pack:{pack.Code.Trim().ToLowerInvariant()}";

        var campaign = await db.Campaigns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.PackageCode == marker, ct);

        if (campaign is null)
        {
            // Adopt an existing draft created by the old scenario provisioner instead of
            // creating a second campaign during the migration to IndustryPack provisioning.
            campaign = await db.Campaigns.SingleOrDefaultAsync(
                x => x.TenantId == tenantId &&
                     x.PackageCode == string.Empty &&
                     x.Name == definition.CampaignName &&
                     x.Status == CampaignStatus.Draft, ct);
        }

        TargetList targetList;

        if (campaign is not null)
        {
            targetList = await db.TargetLists.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaign.TargetListId, ct)
                ?? throw new InvalidOperationException(
                    $"Campaign '{campaign.Id}' references missing target list '{campaign.TargetListId}'.");

            campaign.Name = definition.CampaignName;
            campaign.Objective = definition.Objective;
            campaign.Goal = definition.Goal;
            campaign.SenderName = definition.SenderName;
            campaign.SenderEmail = definition.SenderEmail;
            campaign.TargetListId = targetList.Id;
            targetList.CampaignId = campaign.Id;
            campaign.PackageCode = marker;
            campaign.PackageVersion = Version;
            campaign.PlanStatus = "ready";
            campaign.PlanJson = JsonSerializer.Serialize(definition);
        }
        else
        {
            targetList = new TargetList
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"{definition.IndustryName} Target Accounts",
                Description = $"Provisioned from industry pack '{pack.Code}'.",
                Dynamic = true
            };

            campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TargetListId = targetList.Id,
                Name = definition.CampaignName,
                Goal = definition.Goal,
                Objective = definition.Objective,
                PackageCode = marker,
                PackageVersion = Version,
                PlanStatus = "ready",
                PlanJson = JsonSerializer.Serialize(definition),
                Status = CampaignStatus.Draft,
                SenderName = definition.SenderName,
                SenderEmail = definition.SenderEmail
            };

            targetList.CampaignId = campaign.Id;
            db.TargetLists.Add(targetList);
            db.Campaigns.Add(campaign);
        }

        var icp = await EnsureIcpAsync(tenantId, pack, definition, ct);
        targetList.IcpProfileId = icp.Id;

        var agent = await EnsureCampaignAgentAsync(tenantId, pack, definition, campaign, ct);
        campaign.AgentId = agent.Id;
        var runtimeTemplate = templates.Apply(agent);
        await planner.EnsurePlanAsync(agent, runtimeTemplate, ct, campaign.PlanJson);

        var existingSteps = await db.CampaignSteps
            .Where(x => x.TenantId == tenantId && x.CampaignId == campaign.Id)
            .ToListAsync(ct);

        if (existingSteps.Count > 0)
            db.CampaignSteps.RemoveRange(existingSteps);

        db.CampaignSteps.AddRange(definition.Steps.Select(step => new CampaignStep
        {
            TenantId = tenantId,
            CampaignId = campaign.Id,
            StepNumber = step.StepNumber,
            DelayHours = step.DelayHours,
            Channel = step.Channel,
            SubjectTemplate = step.SubjectTemplate,
            BodyTemplate = step.BodyTemplate,
            RulesJson = JsonSerializer.Serialize(new
            {
                industryPack = pack.Code,
                minimumScore = definition.MinimumScore,
                approvalRequired = true
            })
        }));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new IndustryPackProvisioningResult(
            pack.Id,
            pack.Code,
            targetList.Id,
            campaign.Id,
            campaign.Status.ToString(),
            "industry-pack",
            definition);
        });
    }

    private async Task<AutonomousAcquisitionAgent> EnsureCampaignAgentAsync(
        Guid tenantId,
        IndustryPack pack,
        CampaignReadyDefinition definition,
        Campaign campaign,
        CancellationToken ct)
    {
        var name = $"{definition.CampaignName} Agent";

        var agent = campaign.AgentId.HasValue
            ? await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct)
            : null;

        agent ??= await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Name == name, ct);

        var runtimeConfig = JsonSerializer.Serialize(new
        {
            code = pack.Code,
            name = pack.Name,
            industry = definition.Industry,
            region = definition.Region,
            keywords = definition.Keywords,
            signals = new[] { definition.Industry, pack.Code, "customer acquisition" },
            minimumScore = definition.MinimumScore,
            description = definition.Objective,
            prospectType = "Company",
            targetDefinition = $"Companies matching the {pack.Name} ICP.",
            messages = definition.Steps.Select(x => new
            {
                step = x.StepNumber,
                name = $"Step {x.StepNumber}",
                subject = x.SubjectTemplate,
                body = x.BodyTemplate,
                delayHours = x.DelayHours,
                requiresApproval = true
            })
        });

        if (agent is null)
        {
            agent = new AutonomousAcquisitionAgent
            {
                TenantId = tenantId,
                Name = name,
                TemplateCode = pack.Code,
                Industry = definition.Industry,
                Region = definition.Region,
                CountriesJson = JsonSerializer.Serialize(definition.Countries),
                IcpJson = runtimeConfig,
                MinimumScore = definition.MinimumScore,
                DailyDiscoveryLimit = 50,
                DailyEmailLimit = 10,
                Status = AutonomousAgentStatus.Draft
            };
            db.AutonomousAcquisitionAgents.Add(agent);
        }
        else
        {
            agent.TemplateCode = pack.Code;
            agent.Industry = definition.Industry;
            agent.Region = definition.Region;
            agent.CountriesJson = JsonSerializer.Serialize(definition.Countries);
            agent.IcpJson = runtimeConfig;
            agent.MinimumScore = definition.MinimumScore;
            agent.UpdatedAtUtc = DateTime.UtcNow;
        }

        return agent;
    }

    private async Task<IcpProfile> EnsureIcpAsync(Guid tenantId, IndustryPack pack, CampaignReadyDefinition definition, CancellationToken ct)
    {
        var name = $"{pack.Name} ICP";

        var icp = await db.IcpProfiles.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Name == name, ct);

        if (icp is null)
        {
            icp = new IcpProfile
            {
                TenantId = tenantId,
                Name = name
            };
            db.IcpProfiles.Add(icp);
        }

        icp.Industry = definition.Industry;
        icp.CountriesCsv = string.Join(", ", definition.Countries);
        icp.IntentKeywordsCsv = string.Join(", ", definition.Keywords);
        icp.MinimumEmployees = 0;
        icp.MaximumEmployees = 0;
        icp.CriteriaJson = JsonSerializer.Serialize(new
        {
            minimumScore = definition.MinimumScore,
            source = "industry-pack",
            industryPackId = pack.Id
        });
        icp.Active = true;
        icp.UpdatedAtUtc = DateTime.UtcNow;

        return icp;
    }

    private static CampaignReadyDefinition BuildDefinition(IndustryPack pack, string? scenarioCode)
    {
        var config = Parse(pack.TemplateJson);
        var industry = First(config.Industry, pack.Name);
        var region = First(config.Region, "Europe");
        // Fix for CS0173: ensure both branches return the same type (string[])
        var countries = config.Countries.Count > 0 ? [..config.Countries] : Array.Empty<string>();
        var keywords = config.Keywords.Count > 0
            ? config.Keywords.ToArray()
            : new[] { pack.Name, pack.Code, "customer acquisition" };

        var campaignName = First(config.CampaignName, $"{pack.Name} Acquisition");
        var objective = First(config.Objective, $"Discover, qualify and engage high-fit {industry} prospects.");
        var goal = First(config.Goal, "book-demo");

        var steps = config.Steps.Count > 0
            ? config.Steps.ToArray()
            : new[]
            {
                new CampaignReadyStep(1, 0, "email",
                    "{{company}}: a better way to improve {{pain}}",
                    "Hi {{contact}},\n\nI noticed {{company}} operates in {{industry}}. We help teams improve {{pain}} with a focused workflow.\n\nWould a short introduction be useful?"),
                new CampaignReadyStep(2, 48, "email",
                    "Re: {{company}} and {{pain}}",
                    "Hi {{contact}},\n\nFollowing up on my note about {{pain}}. If this is currently a priority, I can share a concise example of how the workflow works.\n\nWorth a look?"),
                new CampaignReadyStep(3, 120, "email",
                    "Close the loop — {{company}}",
                    "Hi {{contact}},\n\nI will close the loop here. If improving {{pain}} becomes a priority, I would be happy to reconnect.\n\nBest,\n{{sender}}")
            };

        var scenario = Scenario(pack.Code, scenarioCode);
        if (scenario is not null)
        {
            industry = scenario.Industry;
            keywords = scenario.Keywords;
            campaignName = scenario.CampaignName;
            objective = scenario.Objective;
        }

        return new CampaignReadyDefinition(
            pack.Id, pack.Code, pack.Name, industry, region, countries, keywords,
            Math.Clamp(config.MinimumScore, 0, 100), campaignName, objective, goal,
            config.SenderName, config.SenderEmail, steps);
    }


    private sealed record ScenarioDefinition(string Industry, string[] Keywords, string CampaignName, string Objective);

    private static ScenarioDefinition? Scenario(string packCode, string? scenarioCode)
    {
        if (string.IsNullOrWhiteSpace(scenarioCode))
            return null;

        var code = scenarioCode.Trim().ToLowerInvariant();
        if (!packCode.Contains("fusionfleet", StringComparison.OrdinalIgnoreCase) &&
            !packCode.Contains("logistics", StringComparison.OrdinalIgnoreCase))
            return null;

        return code switch
        {
            "logistics-companies" => new("Logistics", ["logistics companies", "logistics providers"], "Find Logistics Companies", "Discover and qualify logistics companies that fit the campaign ICP."),
            "transport-companies" => new("Transport", ["transport companies", "road transport companies"], "Find Transport Companies", "Discover and qualify transport companies that fit the campaign ICP."),
            "freight-forwarders" => new("Freight Forwarding", ["freight forwarders", "freight forwarding companies"], "Find Freight Forwarders", "Discover and qualify freight forwarding companies that fit the campaign ICP."),
            "3pl-providers" => new("3PL", ["3PL providers", "third party logistics companies"], "Find 3PL Providers", "Discover and qualify third-party logistics providers."),
            "warehouse-operators" => new("Warehousing", ["warehouse operators", "warehouse logistics companies"], "Find Warehouse Operators", "Discover and qualify warehouse operators."),
            _ => null
        };
    }

    private static PackConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PackConfig();

        try
        {
            return JsonSerializer.Deserialize<PackConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PackConfig();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("IndustryPack.TemplateJson contains invalid JSON.");
        }
    }

    private static string First(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private sealed class PackConfig
    {
        public string? Industry { get; set; }
        public string? Region { get; set; }
        public List<string> Countries { get; set; } = [];
        public List<string> Keywords { get; set; } = [];
        public int MinimumScore { get; set; } = 70;
        public string? CampaignName { get; set; }
        public string? Objective { get; set; }
        public string? Goal { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public List<CampaignReadyStep> Steps { get; set; } = [];
    }
}
