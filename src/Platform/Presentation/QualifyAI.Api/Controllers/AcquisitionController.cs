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

        return Conflict(new
        {
            code = "verified_source_required",
            detail = "Connect a verified company-data provider or import a licensed/public CSV. QualifyAI does not fabricate prospects."
        });
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

    [HttpPost("prospects/import")][RequirePermission(QualifyAiPermissions.CrmManage)]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> ImportProspects(ProspectImportRequest input, CancellationToken ct)
    {
        if (input.Prospects is null || input.Prospects.Length is < 1 or > 10_000)
            return BadRequest(new { code = "invalid_batch_size", detail = "Import between 1 and 10,000 companies per batch." });
        if (string.IsNullOrWhiteSpace(input.Source) || !input.ComplianceConfirmed)
            return BadRequest(new { code = "source_confirmation_required", detail = "Record the licensed/public source and confirm that this company data may be processed." });

        var tenantId = TenantId;
        var existing = await db.Prospects.AsNoTracking().Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Domain, x.Email }).ToListAsync(ct);
        var domains = existing.Select(x => NormalizeDomain(x.Domain)).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = existing.Select(x => NormalizeEmail(x.Email)).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var accepted = new List<Prospect>(input.Prospects.Length);
        var rejected = 0;
        var duplicates = 0;
        var now = DateTime.UtcNow;

        foreach (var row in input.Prospects)
        {
            var domain = NormalizeDomain(row.Domain);
            var email = NormalizeEmail(row.Email);
            if (string.IsNullOrWhiteSpace(row.CompanyName) || domain.Length == 0)
            {
                rejected++;
                continue;
            }
            if (domains.Contains(domain) || (email.Length > 0 && emails.Contains(email)))
            {
                duplicates++;
                continue;
            }

            var prospect = new Prospect
            {
                TenantId = tenantId,
                CompanyName = row.CompanyName.Trim(),
                Domain = domain,
                ContactName = row.ContactName?.Trim() ?? string.Empty,
                Email = email,
                JobTitle = row.JobTitle?.Trim() ?? string.Empty,
                Industry = row.Industry?.Trim() ?? string.Empty,
                Country = row.Country?.Trim() ?? string.Empty,
                Source = input.Source.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            prospect.Evaluate(row.FitScore, row.IntentScore);
            accepted.Add(prospect);
            domains.Add(domain);
            if (email.Length > 0) emails.Add(email);
        }

        db.Prospects.AddRange(accepted);
        TargetList? targetList = null;
        if (!string.IsNullOrWhiteSpace(input.TargetListName) && accepted.Count > 0)
        {
            if (input.IcpProfileId.HasValue && !await db.IcpProfiles.AnyAsync(x => x.TenantId == tenantId && x.Id == input.IcpProfileId, ct))
                return BadRequest(new { code = "icp_not_found", detail = "The selected ideal customer profile does not belong to this tenant." });

            targetList = new TargetList
            {
                TenantId = tenantId,
                Name = input.TargetListName.Trim(),
                Description = $"Imported from {input.Source.Trim()} on {now:yyyy-MM-dd}. {accepted.Count} unique companies.",
                IcpProfileId = input.IcpProfileId,
                Dynamic = false
            };
            db.TargetLists.Add(targetList);
            db.TargetListMembers.AddRange(accepted.Select(prospect => new TargetListMember
            {
                TenantId = tenantId,
                TargetListId = targetList.Id,
                ProspectId = prospect.Id,
                AddedAtUtc = now
            }));
        }
        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            received = input.Prospects.Length,
            imported = accepted.Count,
            duplicates,
            rejected,
            targetListId = targetList?.Id,
            nextStep = targetList is null ? "create-target-list" : "create-campaign"
        });
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

    [HttpGet("messages")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Messages([FromQuery] OutreachStatus? status, CancellationToken ct)
    {
        var tenantId = TenantId;
        var query = from message in db.OutreachMessages.AsNoTracking()
                    join prospect in db.Prospects.AsNoTracking() on message.ProspectId equals prospect.Id
                    join campaign in db.Campaigns.AsNoTracking() on message.CampaignId equals campaign.Id
                    where message.TenantId == tenantId && (!status.HasValue || message.Status == status.Value)
                    orderby message.CreatedAtUtc descending
                    select new { message.Id, message.CampaignId, campaign = campaign.Name, message.ProspectId, prospect = prospect.CompanyName, prospect.ContactName, prospect.Email, message.Subject, message.Body, message.Status, message.ProviderMessageId, message.SentAtUtc, message.CreatedAtUtc, approvalRequested = db.CrmTasks.Any(task => task.TenantId == tenantId && task.Title == "APPROVAL: Send outreach " + message.Id) };
        return Ok(await query.Take(200).ToListAsync(ct));
    }

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

    private static string NormalizeDomain(string? value)
    {
        var domain = (value ?? string.Empty).Trim().ToLowerInvariant();
        domain = domain.Replace("https://", string.Empty).Replace("http://", string.Empty);
        if (domain.StartsWith("www.")) domain = domain[4..];
        return domain.Split('/')[0].TrimEnd('.');
    }

    private static string NormalizeEmail(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

}

public sealed record TargetListInput(string Name, string Description, Guid? IcpProfileId, bool Dynamic);
public sealed record ProspectImportRequest(string Source, bool ComplianceConfirmed, ProspectImportRow[] Prospects, string? TargetListName = null, Guid? IcpProfileId = null);
public sealed record ProspectImportRow(string CompanyName, string Domain, string? ContactName, string? Email, string? JobTitle, string? Industry, string? Country, int FitScore, int IntentScore);
public sealed record CampaignStepInput(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record CampaignInput(Guid TargetListId, string Name, string Goal, string SenderName, string SenderEmail, DateTime? StartsAtUtc, CampaignStepInput[] Steps);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
