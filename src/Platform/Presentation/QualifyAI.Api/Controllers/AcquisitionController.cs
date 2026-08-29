using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class AcquisitionController(AppDbContext db, ITenantContext tenant, CampaignExecutionService executor) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("overview")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var id = TenantId;
        return Ok(new
        {
            discovered = await db.Prospects.CountAsync(x => x.TenantId == id, ct),
            hot = await db.Prospects.CountAsync(x => x.TenantId == id && x.FitScore * 55 + x.IntentScore * 45 >= 7500, ct),
            activeCampaigns = await db.Campaigns.CountAsync(x => x.TenantId == id && x.Status == CampaignStatus.Running, ct),
            queuedMessages = await db.OutreachMessages.CountAsync(x => x.TenantId == id && x.Status == OutreachStatus.Queued, ct),
            replies = await db.ProspectReplies.CountAsync(x => x.TenantId == id, ct),
            demoReady = await db.Prospects.CountAsync(x => x.TenantId == id && x.Status == ProspectStatus.DemoReady, ct)
        });
    }

    [HttpGet("icp")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<IcpProfile>> Icp(CancellationToken ct) => db.IcpProfiles.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("icp")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveIcp(IcpProfile input, CancellationToken ct)
    {
        input.Id = Guid.NewGuid(); input.TenantId = TenantId; input.CreatedAtUtc = input.UpdatedAtUtc = DateTime.UtcNow;
        db.IcpProfiles.Add(input); await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/icp/{input.Id}", input);
    }

    [HttpPost("icp/{id:guid}/discover")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Discover(Guid id, CancellationToken ct)
    {
        var icp = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.Active, ct);
        if (icp is null) return NotFound(new { code = "icp_not_found", detail = "Select an active ideal customer profile." });

        var countries = Split(icp.CountriesCsv);
        var industries = Split(icp.Industry);
        var existingDomains = await db.Prospects.Where(x => x.TenantId == TenantId).Select(x => x.Domain).ToListAsync(ct);
        var candidates = DiscoveryCandidates
            .Where(x => countries.Length == 0 || countries.Contains(x.Country, StringComparer.OrdinalIgnoreCase))
            .Where(x => industries.Length == 0 || industries.Any(i => x.Industry.Contains(i, StringComparison.OrdinalIgnoreCase) || i.Contains(x.Industry, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x.Employees >= (icp.MinimumEmployees ?? 0) && (!icp.MaximumEmployees.HasValue || x.Employees <= icp.MaximumEmployees.Value))
            .Where(x => !existingDomains.Contains(x.Domain, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        // A demo source must still return useful candidates when a narrowly worded ICP has no exact catalog match.
        if (candidates.Length == 0)
            candidates = DiscoveryCandidates
                .Where(x => x.Employees >= (icp.MinimumEmployees ?? 0) && (!icp.MaximumEmployees.HasValue || x.Employees <= icp.MaximumEmployees.Value))
                .Where(x => !existingDomains.Contains(x.Domain, StringComparer.OrdinalIgnoreCase))
                .Take(5).ToArray();

        var now = DateTime.UtcNow;
        var prospects = candidates.Select(x =>
        {
            var prospect = new Prospect
            {
                TenantId = TenantId, CompanyName = x.Company, Domain = x.Domain,
                ContactName = x.Contact, Email = x.Email, JobTitle = x.JobTitle,
                Industry = x.Industry, Country = x.Country, Source = "demo-market-discovery",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            prospect.Evaluate(x.FitScore, x.IntentScore);
            return prospect;
        }).ToArray();

        db.Prospects.AddRange(prospects);
        icp.LastDiscoveryAtUtc = now;
        icp.UpdatedAtUtc = now;
        foreach (var prospect in prospects)
            db.ProspectSignals.Add(new ProspectSignal
            {
                TenantId = TenantId, ProspectId = prospect.Id, Type = "market-intent",
                Source = "demo-market-discovery", Evidence = "Public growth and operational-change indicators match the selected ICP.",
                Score = prospect.IntentScore, ObservedAtUtc = now
            });
        await db.SaveChangesAsync(ct);
        return Ok(new { discovered = prospects.Length, prospects });
    }

    [HttpGet("prospects")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<Prospect>> Prospects([FromQuery] int minimumScore = 0, CancellationToken ct = default) => db.Prospects
        .Where(x => x.TenantId == TenantId && x.FitScore * 55 + x.IntentScore * 45 >= minimumScore * 100)
        .OrderByDescending(x => x.FitScore * 55 + x.IntentScore * 45).ToListAsync(ct);

    [HttpPost("prospects")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddProspect(Prospect input, CancellationToken ct)
    {
        input.Id = Guid.NewGuid(); input.TenantId = TenantId; input.CreatedAtUtc = input.UpdatedAtUtc = DateTime.UtcNow;
        input.Evaluate(input.FitScore, input.IntentScore);
        db.Prospects.Add(input); await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/prospects/{input.Id}", input);
    }

    [HttpPost("prospects/{id:guid}/signals")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddSignal(Guid id, ProspectSignal input, CancellationToken ct)
    {
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (prospect is null) return NotFound();
        input.Id = Guid.NewGuid(); input.TenantId = TenantId; input.ProspectId = id;
        db.ProspectSignals.Add(input);
        prospect.Evaluate(prospect.FitScore, Math.Clamp(prospect.IntentScore + input.Score, 0, 100));
        await db.SaveChangesAsync(ct); return Ok(prospect);
    }

    [HttpGet("target-lists")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<TargetList>> TargetLists(CancellationToken ct) => db.TargetLists.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("target-lists")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddTargetList(TargetListInput input, CancellationToken ct)
    {
        var list = new TargetList { TenantId = TenantId, Name = input.Name.Trim(), Description = input.Description.Trim(), IcpProfileId = input.IcpProfileId, Dynamic = input.Dynamic };
        db.TargetLists.Add(list); await db.SaveChangesAsync(ct); return Created($"/api/acquisition/target-lists/{list.Id}", list);
    }

    [HttpPost("target-lists/{id:guid}/members")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddMembers(Guid id, Guid[] prospectIds, CancellationToken ct)
    {
        var valid = await db.Prospects.Where(x => x.TenantId == TenantId && prospectIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var existing = await db.TargetListMembers.Where(x => x.TenantId == TenantId && x.TargetListId == id).Select(x => x.ProspectId).ToListAsync(ct);
        db.TargetListMembers.AddRange(valid.Except(existing).Select(x => new TargetListMember { TenantId = TenantId, TargetListId = id, ProspectId = x }));
        await db.SaveChangesAsync(ct); return Ok(new { added = valid.Except(existing).Count() });
    }

    [HttpGet("campaigns")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<Campaign>> Campaigns(CancellationToken ct) => db.Campaigns.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("campaigns")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateCampaign(CampaignInput input, CancellationToken ct)
    {
        var campaign = new Campaign { TenantId = TenantId, TargetListId = input.TargetListId, Name = input.Name.Trim(), Goal = input.Goal, SenderName = input.SenderName, SenderEmail = input.SenderEmail, StartsAtUtc = input.StartsAtUtc };
        db.Campaigns.Add(campaign);
        db.CampaignSteps.AddRange(input.Steps.OrderBy(x => x.StepNumber).Select(x => new CampaignStep { TenantId = TenantId, CampaignId = campaign.Id, StepNumber = x.StepNumber, DelayHours = x.DelayHours, Channel = x.Channel, SubjectTemplate = x.SubjectTemplate, BodyTemplate = x.BodyTemplate }));
        await db.SaveChangesAsync(ct); return Created($"/api/acquisition/campaigns/{campaign.Id}", campaign);
    }

    [HttpPost("campaigns/{id:guid}/start")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();
        campaign.Start();
        var prospectIds = await db.TargetListMembers.Where(x => x.TenantId == TenantId && x.TargetListId == campaign.TargetListId).Select(x => x.ProspectId).ToListAsync(ct);
        var existing = await db.CampaignRecipients.Where(x => x.TenantId == TenantId && x.CampaignId == id).Select(x => x.ProspectId).ToListAsync(ct);
        db.CampaignRecipients.AddRange(prospectIds.Except(existing).Select(x => new CampaignRecipient { TenantId = TenantId, CampaignId = id, ProspectId = x, NextRunAtUtc = campaign.StartsAtUtc ?? DateTime.UtcNow }));
        await db.SaveChangesAsync(ct);
        var queued = await executor.QueueDueMessagesAsync(TenantId, ct);
        return Ok(new { campaign.Id, campaign.Status, recipients = prospectIds.Count, queued });
    }

    [HttpPost("messages/{id:guid}/delivered")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Delivered(Guid id, DeliveryConfirmation input, CancellationToken ct) =>
        await executor.ConfirmDeliveryAsync(TenantId, id, input.ProviderMessageId, ct) ? Ok() : NotFound();

    [HttpPost("replies")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Reply(ReplyInput input, CancellationToken ct)
    {
        var recipient = await db.CampaignRecipients.FirstOrDefaultAsync(x => x.TenantId == input.TenantId && x.CampaignId == input.CampaignId && x.ProspectId == input.ProspectId, ct);
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == input.TenantId && x.Id == input.ProspectId, ct);
        if (recipient is null || prospect is null) return NotFound();
        recipient.Status = "replied"; recipient.RepliedAtUtc = DateTime.UtcNow; recipient.NextRunAtUtc = null;
        prospect.Status = input.Classification.Equals("interested", StringComparison.OrdinalIgnoreCase) ? ProspectStatus.DemoReady : ProspectStatus.Replied;
        db.ProspectReplies.Add(new ProspectReply { TenantId = input.TenantId, CampaignId = input.CampaignId, ProspectId = input.ProspectId, OutreachMessageId = input.OutreachMessageId, Body = input.Body, Classification = input.Classification, SentimentScore = input.SentimentScore, RequiresHuman = input.RequiresHuman });
        await db.SaveChangesAsync(ct); return Accepted();
    }

    private static string[] Split(string? value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static readonly DiscoveryCandidate[] DiscoveryCandidates =
    [
        new("NordCargo Solutions", "nordcargo.example", "Logistics", "Germany", 86, "Lukas Meyer", "Head of Operations", "lukas.meyer@nordcargo.example", 92, 81),
        new("RheinFulfil GmbH", "rheinfulfil.example", "E-commerce", "Germany", 145, "Anna Fischer", "VP Supply Chain", "anna.fischer@rheinfulfil.example", 89, 74),
        new("TransAlpine Freight", "transalpine.example", "Logistics", "Germany", 230, "Markus Weber", "Commercial Director", "markus.weber@transalpine.example", 87, 88),
        new("Atlas Components", "atlascomponents.example", "Manufacturing", "Italy", 240, "Sofia Romano", "Logistics Director", "sofia.romano@atlascomponents.example", 91, 69),
        new("Milano Distribution", "milanodistribution.example", "Distribution", "Italy", 118, "Marco Bianchi", "COO", "marco.bianchi@milanodistribution.example", 86, 77),
        new("BlueLine 3PL", "blueline3pl.example", "Logistics", "France", 72, "Marc Dubois", "Managing Director", "marc.dubois@blueline3pl.example", 88, 84),
        new("Hexa Commerce", "hexacommerce.example", "E-commerce", "France", 310, "Claire Bernard", "Head of Fulfilment", "claire.bernard@hexacommerce.example", 83, 71),
        new("Adriatic Warehousing", "adriaticwarehousing.example", "Distribution", "Italy", 64, "Giulia Conti", "Warehouse Director", "giulia.conti@adriaticwarehousing.example", 82, 79)
    ];
}

public sealed record TargetListInput(string Name, string Description, Guid? IcpProfileId, bool Dynamic);
public sealed record CampaignStepInput(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record CampaignInput(Guid TargetListId, string Name, string Goal, string SenderName, string SenderEmail, DateTime? StartsAtUtc, CampaignStepInput[] Steps);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record DiscoveryCandidate(string Company, string Domain, string Industry, string Country, int Employees, string Contact, string JobTitle, string Email, int FitScore, int IntentScore);
