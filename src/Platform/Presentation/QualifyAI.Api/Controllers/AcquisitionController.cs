using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Api.Importing;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class AcquisitionController(
    AppDbContext db,
    ITenantContext tenant,
    CampaignExecutionService executor,
    ProspectReplyProcessingService replyProcessor,
    ProspectDiscoveryService discovery) : ControllerBase
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

    [HttpGet("discovery/providers")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public IActionResult DiscoveryProviders() => Ok(discovery.ProviderStatus());

    [HttpPost("icp/{id:guid}/discover")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Discover(Guid id, [FromBody] DiscoveryRequest? input, CancellationToken ct)
    {
        try
        {
            var request = input ?? new DiscoveryRequest();
            var result = await discovery.DiscoverAsync(TenantId, id, new DiscoveryRunOptions(
                request.Source, request.Region, request.MaximumResults, request.MinimumScore,
                request.TargetListName, request.CreateTargetList), ct);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { code = "discovery_not_ready", detail = exception.Message });
        }
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
        var existing = await db.Prospects.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var byDomain = existing.Where(x => NormalizeDomain(x.Domain).Length > 0)
            .GroupBy(x => NormalizeDomain(x.Domain), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var byEmail = existing.Where(x => NormalizeEmail(x.Email).Length > 0)
            .GroupBy(x => NormalizeEmail(x.Email), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var included = new List<Prospect>(input.Prospects.Length);
        var created = new List<Prospect>(input.Prospects.Length);
        var includedIds = new HashSet<Guid>();
        var rejected = 0;
        var duplicates = 0;
        var updated = 0;
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
            var hasDomain = byDomain.TryGetValue(domain, out var existingProspect);
            if (!hasDomain && email.Length > 0)
                byEmail.TryGetValue(email, out existingProspect);

            if (existingProspect is not null)
            {
                if (!includedIds.Add(existingProspect.Id))
                {
                    duplicates++;
                    continue;
                }
                MergeImportedProspect(existingProspect, row, domain, email, input.Source, now);
                included.Add(existingProspect);
                updated++;
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
                Source = string.IsNullOrWhiteSpace(row.Source) ? input.Source.Trim() : row.Source.Trim(),
                Priority = row.Priority?.Trim() ?? string.Empty,
                ContactReadiness = row.ContactReadiness?.Trim() ?? string.Empty,
                SuggestedBuyer = row.SuggestedBuyer?.Trim() ?? string.Empty,
                SizeBand = row.SizeBand?.Trim() ?? string.Empty,
                PainHypothesis = row.PainHypothesis?.Trim() ?? string.Empty,
                Offer = row.Offer?.Trim() ?? string.Empty,
                SourceUrl = row.SourceUrl?.Trim() ?? string.Empty,
                VerificationStatus = row.VerificationStatus?.Trim() ?? string.Empty,
                OutreachStatus = row.OutreachStatus?.Trim() ?? string.Empty,
                DatasetOrigin = row.DatasetOrigin?.Trim() ?? string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            prospect.Evaluate(row.FitScore, row.IntentScore);
            included.Add(prospect);
            created.Add(prospect);
            includedIds.Add(prospect.Id);
            byDomain[domain] = prospect;
            if (email.Length > 0) byEmail[email] = prospect;
        }

        db.Prospects.AddRange(created);
        TargetList? targetList = null;
        if (!string.IsNullOrWhiteSpace(input.TargetListName) && included.Count > 0)
        {
            if (input.IcpProfileId.HasValue && !await db.IcpProfiles.AnyAsync(x => x.TenantId == tenantId && x.Id == input.IcpProfileId, ct))
                return BadRequest(new { code = "icp_not_found", detail = "The selected ideal customer profile does not belong to this tenant." });

            targetList = new TargetList
            {
                TenantId = tenantId,
                Name = input.TargetListName.Trim(),
                Description = $"Imported from {input.Source.Trim()} on {now:yyyy-MM-dd}. {included.Count} unique companies.",
                IcpProfileId = input.IcpProfileId,
                Dynamic = false
            };
            db.TargetLists.Add(targetList);
            db.TargetListMembers.AddRange(included.Select(prospect => new TargetListMember
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
            imported = included.Count - updated,
            updated,
            duplicates,
            rejected,
            targetListId = targetList?.Id,
            nextStep = targetList is null ? "create-target-list" : "create-campaign"
        });
    }

    [HttpPost("prospects/import/preview")][RequirePermission(QualifyAiPermissions.CrmManage)]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> PreviewImport([FromForm] IFormFile? file, [FromForm] string? sheetName, [FromForm] int? headerRow, CancellationToken ct)
    {
        if (file is null) return BadRequest(new { code = "import_file_required", detail = "Choose a CSV or XLSX file." });
        try { return Ok(await ProspectDatasetReader.ReadAsync(file, sheetName, headerRow, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { code = "invalid_import_file", detail = ex.Message }); }
        catch (InvalidDataException) { return BadRequest(new { code = "invalid_xlsx", detail = "The XLSX file is damaged or cannot be read." }); }
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
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaigns = await db.Campaigns.AsNoTracking().Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.TargetListId,
                campaign.Name,
                campaign.Goal,
                campaign.Status,
                campaign.SenderName,
                campaign.SenderEmail,
                campaign.StartsAtUtc,
                campaign.CreatedAtUtc,
                recipients = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id),
                active = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "active"),
                awaitingDelivery = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "awaiting-delivery"),
                replied = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "replied"),
                completed = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "completed"),
                failed = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "failed"),
                queued = db.OutreachMessages.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == OutreachStatus.Queued),
                sent = db.OutreachMessages.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && (x.Status == OutreachStatus.Sent || x.Status == OutreachStatus.Delivered || x.Status == OutreachStatus.Replied))
            }).ToListAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("campaigns/{id:guid}/activity")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> CampaignActivity(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        if (!await db.Campaigns.AnyAsync(x => x.TenantId == tenantId && x.Id == id, ct)) return NotFound();
        var messageRows = await db.OutreachMessages.AsNoTracking().Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(100)
            .Select(x => new { x.Id, x.UpdatedAtUtc, x.Status, x.Subject, x.ProviderMessageId }).ToListAsync(ct);
        var messages = messageRows.Select(x => new CampaignActivityItem(x.Id, x.UpdatedAtUtc, "message", x.Status.ToString(), x.Subject, x.ProviderMessageId));
        var replies = await db.ProspectReplies.AsNoTracking().Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .OrderByDescending(x => x.ReceivedAtUtc).Take(100)
            .Select(x => new CampaignActivityItem(x.Id, x.ReceivedAtUtc, "reply", x.Classification, "Prospect reply", x.Body)).ToListAsync(ct);
        return Ok(messages.Concat(replies).OrderByDescending(x => x.AtUtc).Take(100));
    }

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
        try
        {
            var result = await replyProcessor.ProcessAsync(TenantId, new ProcessProspectReplyRequest(
                input.CampaignId, input.ProspectId, input.OutreachMessageId, input.Body,
                input.Classification, input.SentimentScore, input.RequiresHuman), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { detail = exception.Message });
        }
    }

    private static void MergeImportedProspect(Prospect prospect, ProspectImportRow row, string domain, string email, string batchSource, DateTime now)
    {
        prospect.CompanyName = Prefer(row.CompanyName, prospect.CompanyName);
        prospect.Domain = Prefer(domain, prospect.Domain);
        prospect.ContactName = Prefer(row.ContactName, prospect.ContactName);
        prospect.Email = Prefer(email, prospect.Email);
        prospect.JobTitle = Prefer(row.JobTitle, prospect.JobTitle);
        prospect.Industry = Prefer(row.Industry, prospect.Industry);
        prospect.Country = Prefer(row.Country, prospect.Country);
        prospect.Source = Prefer(row.Source, Prefer(batchSource, prospect.Source));
        prospect.Priority = Prefer(row.Priority, prospect.Priority);
        prospect.ContactReadiness = Prefer(row.ContactReadiness, prospect.ContactReadiness);
        prospect.SuggestedBuyer = Prefer(row.SuggestedBuyer, prospect.SuggestedBuyer);
        prospect.SizeBand = Prefer(row.SizeBand, prospect.SizeBand);
        prospect.PainHypothesis = Prefer(row.PainHypothesis, prospect.PainHypothesis);
        prospect.Offer = Prefer(row.Offer, prospect.Offer);
        prospect.SourceUrl = Prefer(row.SourceUrl, prospect.SourceUrl);
        prospect.VerificationStatus = Prefer(row.VerificationStatus, prospect.VerificationStatus);
        prospect.OutreachStatus = Prefer(row.OutreachStatus, prospect.OutreachStatus);
        prospect.DatasetOrigin = Prefer(row.DatasetOrigin, prospect.DatasetOrigin);
        prospect.Evaluate(row.FitScore, row.IntentScore);
        prospect.UpdatedAtUtc = now;
    }

    private static string Prefer(string? incoming, string? fallback)
        => string.IsNullOrWhiteSpace(incoming) ? fallback?.Trim() ?? string.Empty : incoming.Trim();

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
public sealed record DiscoveryRequest(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true);
public sealed record ProspectImportRequest(string Source, bool ComplianceConfirmed, ProspectImportRow[] Prospects, string? TargetListName = null, Guid? IcpProfileId = null);
public sealed record ProspectImportRow(
    string CompanyName,
    string Domain,
    string? ContactName,
    string? Email,
    string? JobTitle,
    string? Industry,
    string? Country,
    string? Source,
    int FitScore,
    int IntentScore,
    string? Priority = null,
    string? ContactReadiness = null,
    string? SuggestedBuyer = null,
    string? SizeBand = null,
    string? PainHypothesis = null,
    string? Offer = null,
    string? SourceUrl = null,
    string? VerificationStatus = null,
    string? OutreachStatus = null,
    string? DatasetOrigin = null);
public sealed record CampaignStepInput(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record CampaignInput(Guid TargetListId, string Name, string Goal, string SenderName, string SenderEmail, DateTime? StartsAtUtc, CampaignStepInput[] Steps);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record CampaignActivityItem(Guid Id, DateTime AtUtc, string Type, string Status, string Title, string Detail);
