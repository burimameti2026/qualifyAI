using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Api.Importing;

namespace LeadsAI.Api.Controllers;

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
            discovered = await db.Prospects.CountAsync(x => x.TenantId==id, ct),
            hot = await db.Prospects.CountAsync(x => x.TenantId==id&&x.FitScore*55+x.IntentScore*45>=7500, ct),
            activeCampaigns = await db.Campaigns.CountAsync(x => x.TenantId==id&&x.Status==CampaignStatus.Running, ct),
            queuedMessages = await db.OutreachMessages.CountAsync(x => x.TenantId==id&&x.Status==OutreachStatus.Queued, ct),
            replies = await db.ProspectReplies.CountAsync(x => x.TenantId==id, ct),
            demoReady = await db.Prospects.CountAsync(x => x.TenantId==id&&x.Status==ProspectStatus.DemoReady, ct)
        });
    }

    [HttpGet("icp")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<IcpProfile>> Icp(CancellationToken ct) => db.IcpProfiles.Where(x => x.TenantId==TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("icp")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveIcp(IcpProfile input, CancellationToken ct)
    {
        input.Id=Guid.NewGuid(); input.TenantId=TenantId; input.CreatedAtUtc=input.UpdatedAtUtc=DateTime.UtcNow;
        db.IcpProfiles.Add(input); await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/icp/{input.Id}", input);
    }

    [HttpGet("discovery/providers")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public IActionResult DiscoveryProviders() => Ok(discovery.ProviderStatus());

    [HttpPost("discovery/providers/{name}/verify")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> VerifyDiscoveryProvider(string name, CancellationToken ct)
    {
        try
        {
            var result = await discovery.VerifyProviderAsync(name, ct);
            return result.Verified
                ? Ok(result)
                : BadRequest(result);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new
            {
                code = "discovery_provider_not_found",
                detail = exception.Message
            });
        }
    }

    [HttpPost("icp/{id:guid}/discover")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Discover(Guid id, [FromBody] DiscoveryRequest? input, CancellationToken ct)
    {
        try
        {
            var request = input??new DiscoveryRequest();
            var result = await discovery.DiscoverAsync(TenantId, id, new DiscoveryRunOptions(
                request.Source, request.Region, request.MaximumResults, request.MinimumScore,
                request.TargetListName, request.CreateTargetList, request.CountriesCsv), ct);
            return Ok(result);
        }
        catch(InvalidOperationException exception)
        {
            return Conflict(new { code = "discovery_not_ready", detail = exception.Message });
        }
    }

    [HttpGet("prospects")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<Prospect>> Prospects([FromQuery] int minimumScore = 0, CancellationToken ct = default) => db.Prospects
        .Where(x => x.TenantId==TenantId&&x.FitScore*55+x.IntentScore*45>=minimumScore*100)
        .OrderByDescending(x => x.FitScore*55+x.IntentScore*45).ToListAsync(ct);

    [HttpPost("prospects")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddProspect(Prospect input, CancellationToken ct)
    {
        input.Id=Guid.NewGuid(); input.TenantId=TenantId; input.CreatedAtUtc=input.UpdatedAtUtc=DateTime.UtcNow;
        input.Evaluate(input.FitScore, input.IntentScore);
        db.Prospects.Add(input); await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/prospects/{input.Id}", input);
    }

    [HttpPost("prospects/import")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> ImportProspects(ProspectImportRequest input, CancellationToken ct)
    {
        if(input.Prospects is null||input.Prospects.Length is <1 or >10_000)
            return BadRequest(new { code = "invalid_batch_size", detail = "Import between 1 and 10,000 companies per batch." });
        if(string.IsNullOrWhiteSpace(input.Source)||!input.ComplianceConfirmed)
            return BadRequest(new { code = "source_confirmation_required", detail = "Record the licensed/public source and confirm that this company data may be processed." });

        var tenantId = TenantId;
        var existing = await db.Prospects.Where(x => x.TenantId==tenantId).ToListAsync(ct);
        var byDomain = existing.Where(x => NormalizeDomain(x.Domain).Length>0)
            .GroupBy(x => NormalizeDomain(x.Domain), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var byEmail = existing.Where(x => NormalizeEmail(x.Email).Length>0)
            .GroupBy(x => NormalizeEmail(x.Email), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var included = new List<Prospect>(input.Prospects.Length);
        var created = new List<Prospect>(input.Prospects.Length);
        var includedIds = new HashSet<Guid>();
        var rejected = 0;
        var duplicates = 0;
        var updated = 0;
        var now = DateTime.UtcNow;

        foreach(var row in input.Prospects)
        {
            var domain = NormalizeDomain(row.Domain);
            var email = NormalizeEmail(row.Email);
            if(string.IsNullOrWhiteSpace(row.CompanyName)||domain.Length==0)
            {
                rejected++;
                continue;
            }
            var hasDomain = byDomain.TryGetValue(domain, out var existingProspect);
            if(!hasDomain&&email.Length>0)
                byEmail.TryGetValue(email, out existingProspect);

            if(existingProspect is not null)
            {
                if(!includedIds.Add(existingProspect.Id))
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
                TenantId=tenantId,
                CompanyName=row.CompanyName.Trim(),
                Domain=domain,
                ContactName=row.ContactName?.Trim()??string.Empty,
                Email=email,
                JobTitle=row.JobTitle?.Trim()??string.Empty,
                Industry=row.Industry?.Trim()??string.Empty,
                Country=row.Country?.Trim()??string.Empty,
                Source=string.IsNullOrWhiteSpace(row.Source) ? input.Source.Trim() : row.Source.Trim(),
                Priority=row.Priority?.Trim()??string.Empty,
                ContactReadiness=row.ContactReadiness?.Trim()??string.Empty,
                SuggestedBuyer=row.SuggestedBuyer?.Trim()??string.Empty,
                SizeBand=row.SizeBand?.Trim()??string.Empty,
                PainHypothesis=row.PainHypothesis?.Trim()??string.Empty,
                Offer=row.Offer?.Trim()??string.Empty,
                SourceUrl=row.SourceUrl?.Trim()??string.Empty,
                VerificationStatus=row.VerificationStatus?.Trim()??string.Empty,
                OutreachStatus=row.OutreachStatus?.Trim()??string.Empty,
                DatasetOrigin=row.DatasetOrigin?.Trim()??string.Empty,
                CreatedAtUtc=now,
                UpdatedAtUtc=now
            };
            prospect.Evaluate(row.FitScore, row.IntentScore);
            included.Add(prospect);
            created.Add(prospect);
            includedIds.Add(prospect.Id);
            byDomain[domain]=prospect;
            if(email.Length>0) byEmail[email]=prospect;
        }

        db.Prospects.AddRange(created);
        TargetList? targetList = null;
        if(!string.IsNullOrWhiteSpace(input.TargetListName)&&included.Count>0)
        {
            if(input.IcpProfileId.HasValue&&!await db.IcpProfiles.AnyAsync(x => x.TenantId==tenantId&&x.Id==input.IcpProfileId, ct))
                return BadRequest(new { code = "icp_not_found", detail = "The selected ideal customer profile does not belong to this tenant." });

            targetList=new TargetList
            {
                TenantId=tenantId,
                Name=input.TargetListName.Trim(),
                Description=$"Imported from {input.Source.Trim()} on {now:yyyy-MM-dd}. {included.Count} unique companies.",
                IcpProfileId=input.IcpProfileId,
                Dynamic=false
            };
            db.TargetLists.Add(targetList);
            db.TargetListMembers.AddRange(included.Select(prospect => new TargetListMember
            {
                TenantId=tenantId,
                TargetListId=targetList.Id,
                ProspectId=prospect.Id,
                AddedAtUtc=now
            }));
        }
        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            received = input.Prospects.Length,
            imported = included.Count-updated,
            updated,
            duplicates,
            rejected,
            targetListId = targetList?.Id,
            nextStep = targetList is null ? "create-target-list" : "create-campaign"
        });
    }

    [HttpPost("prospects/import/preview")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> PreviewImport([FromForm] IFormFile? file, [FromForm] string? sheetName, [FromForm] int? headerRow, CancellationToken ct)
    {
        if(file is null) return BadRequest(new { code = "import_file_required", detail = "Choose a CSV or XLSX file." });
        try { return Ok(await ProspectDatasetReader.ReadAsync(file, sheetName, headerRow, ct)); }
        catch(InvalidOperationException ex) { return BadRequest(new { code = "invalid_import_file", detail = ex.Message }); }
        catch(InvalidDataException) { return BadRequest(new { code = "invalid_xlsx", detail = "The XLSX file is damaged or cannot be read." }); }
    }

    [HttpPost("prospects/{id:guid}/signals")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddSignal(Guid id, ProspectSignal input, CancellationToken ct)
    {
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId==TenantId&&x.Id==id, ct);
        if(prospect is null) return NotFound();
        input.Id=Guid.NewGuid(); input.TenantId=TenantId; input.ProspectId=id;
        db.ProspectSignals.Add(input);
        prospect.Evaluate(prospect.FitScore, Math.Clamp(prospect.IntentScore+input.Score, 0, 100));
        await db.SaveChangesAsync(ct); return Ok(prospect);
    }

    [HttpGet("templates")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        var tenantId = TenantId;
        var templates = await db.OutreachTemplates.Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        if (templates.Count == 0)
        {
            templates = CreateDefaultTemplates(tenantId);
            db.OutreachTemplates.AddRange(templates);
            await db.SaveChangesAsync(ct);
        }
        return Ok(templates.Select(x => new { x.Id, x.Name, x.Description, x.SubjectTemplate, x.BodyTemplate, x.IsActive }));
    }

    [HttpPost("templates")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateTemplate(OutreachTemplateInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.SubjectTemplate) || string.IsNullOrWhiteSpace(input.BodyTemplate))
            return BadRequest(new { detail = "Template name, subject and body are required." });
        var name = input.Name.Trim();
        if (await db.OutreachTemplates.AnyAsync(x => x.TenantId == TenantId && x.Name == name, ct))
            return Conflict(new { detail = "A template with this name already exists." });
        var template = new OutreachTemplate { TenantId = TenantId, Name = name, Description = input.Description?.Trim() ?? string.Empty, SubjectTemplate = input.SubjectTemplate.Trim(), BodyTemplate = input.BodyTemplate.Trim(), IsActive = true };
        db.OutreachTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/templates/{template.Id}", template);
    }

    [HttpPut("templates/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateTemplate(Guid id, OutreachTemplateInput input, CancellationToken ct)
    {
        var template = await db.OutreachTemplates.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (template is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.SubjectTemplate) || string.IsNullOrWhiteSpace(input.BodyTemplate))
            return BadRequest(new { detail = "Template name, subject and body are required." });
        var name = input.Name.Trim();
        if (await db.OutreachTemplates.AnyAsync(x => x.TenantId == TenantId && x.Id != id && x.Name == name, ct))
            return Conflict(new { detail = "A template with this name already exists." });
        template.Name = name; template.Description = input.Description?.Trim() ?? string.Empty; template.SubjectTemplate = input.SubjectTemplate.Trim(); template.BodyTemplate = input.BodyTemplate.Trim(); template.IsActive = true;
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpDelete("templates/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        var template = await db.OutreachTemplates.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (template is null) return NotFound();
        template.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Ok(new { template.Id, template.IsActive });
    }

    [HttpGet("target-lists")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<TargetList>> TargetLists(CancellationToken ct) => db.TargetLists.Where(x => x.TenantId==TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("target-lists")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddTargetList(TargetListInput input, CancellationToken ct)
    {
        var list = new TargetList { TenantId=TenantId, Name=input.Name.Trim(), Description=input.Description.Trim(), IcpProfileId=input.IcpProfileId, Dynamic=input.Dynamic };
        db.TargetLists.Add(list); await db.SaveChangesAsync(ct); return Created($"/api/acquisition/target-lists/{list.Id}", list);
    }

    [HttpPost("target-lists/{id:guid}/members")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddMembers(Guid id, Guid[] prospectIds, CancellationToken ct)
    {
        var valid = await db.Prospects.Where(x => x.TenantId==TenantId&&prospectIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var existing = await db.TargetListMembers.Where(x => x.TenantId==TenantId&&x.TargetListId==id).Select(x => x.ProspectId).ToListAsync(ct);
        db.TargetListMembers.AddRange(valid.Except(existing).Select(x => new TargetListMember { TenantId=TenantId, TargetListId=id, ProspectId=x }));
        await db.SaveChangesAsync(ct); return Ok(new { added = valid.Except(existing).Count() });
    }

    [HttpGet("campaigns")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaigns = await db.Campaigns.AsNoTracking().Where(x => x.TenantId==tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.TargetListId,
                campaign.Name,
                campaign.Goal,
                campaign.Objective,
                campaign.Status,
                campaign.SenderName,
                campaign.SenderEmail,
                campaign.StartsAtUtc,
                campaign.CreatedAtUtc,
                campaign.UpdatedAtUtc,
                campaign.PackageCode,
                campaign.PackageVersion,
                campaign.PlanStatus,
                campaign.PlanJson,
                campaign.AgentId,
                recipients = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id),
                active = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="active"),
                awaitingDelivery = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="awaiting-delivery"),
                replied = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="replied"),
                completed = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="completed"),
                failed = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="failed"),
                queued = db.OutreachMessages.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==OutreachStatus.Queued),
                sent = db.OutreachMessages.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&(x.Status==OutreachStatus.Sent||x.Status==OutreachStatus.Delivered||x.Status==OutreachStatus.Replied))
            }).ToListAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("campaigns/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> CampaignDetail(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaign = await db.Campaigns.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new { x.Id, x.TargetListId, x.Name, x.Goal, x.Status, x.SenderName, x.SenderEmail, x.StartsAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc })
            .SingleOrDefaultAsync(ct);
        if (campaign is null) return NotFound();
        var steps = await db.CampaignSteps.AsNoTracking().Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .OrderBy(x => x.StepNumber)
            .Select(x => new { x.Id, x.StepNumber, x.DelayHours, x.Channel, x.SubjectTemplate, x.BodyTemplate, x.TemplateId, templateName = db.OutreachTemplates.Where(t => t.TenantId == tenantId && t.Id == x.TemplateId).Select(t => t.Name).FirstOrDefault() })
            .ToListAsync(ct);

        var targetList = await db.TargetLists.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == campaign.TargetListId)
            .Select(x => new { x.Id, x.Name, x.Description, x.CampaignId, x.IcpProfileId, x.Dynamic })
            .SingleOrDefaultAsync(ct);

        var icpId = targetList?.IcpProfileId;
        var icp = icpId.HasValue
            ? await db.IcpProfiles.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == icpId.Value)
                .Select(x => new { x.Id, x.Name, x.Industry, x.CountriesCsv, x.IntentKeywordsCsv, x.MinimumEmployees, x.MaximumEmployees, x.CriteriaJson, x.Active })
                .SingleOrDefaultAsync(ct)
            : null;

        var prospects = await (
            from member in db.TargetListMembers.AsNoTracking()
            join prospect in db.Prospects.AsNoTracking() on member.ProspectId equals prospect.Id
            where member.TenantId == tenantId && member.TargetListId == campaign.TargetListId && prospect.TenantId == tenantId
            orderby prospect.PriorityScore descending, prospect.CompanyName
            select new
            {
                prospect.Id,
                prospect.CompanyName,
                prospect.Domain,
                prospect.ContactName,
                prospect.Email,
                prospect.JobTitle,
                prospect.Industry,
                prospect.Country,
                prospect.Status,
                prospect.FitScore,
                prospect.IntentScore,
                prospect.PriorityScore,
                prospect.Source,
                prospect.UpdatedAtUtc
            }).Take(500).ToListAsync(ct);

        var latestRun = await db.AutonomousAcquisitionAgentRuns.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .OrderByDescending(x => x.ScheduledAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.ScheduledAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.DiscoveredCount,
                x.QualifiedCount,
                x.HighScoreCount,
                x.EmailsSentCount,
                x.Error
            })
            .FirstOrDefaultAsync(ct);

        var tasks = latestRun is null
            ? Enumerable.Empty<object>().ToList()
            : (await db.AutonomousAcquisitionTasks.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.RunId == latestRun.Id)
                .OrderBy(x => x.Sequence)
                .Select(x => new
                {
                    x.Id,
                    x.Sequence,
                    x.Type,
                    x.Name,
                    x.Status,
                    x.RequiresApproval,
                    x.StartedAtUtc,
                    x.CompletedAtUtc,
                    x.ResultJson,
                    x.Error
                })
                .ToListAsync(ct)).Cast<object>().ToList();

        return Ok(new { campaign, steps, icp, targetList, prospects, latestRun, tasks });
    }

    [HttpPost("campaigns/{id:guid}/pause")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();
        try
        {
            campaign.Status = CampaignStatus.Paused;
            if (campaign.AgentId.HasValue)
            {
                var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                    x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
                if (agent is not null)
                {
                    agent.Status = AutonomousAgentStatus.Paused;
                    agent.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
            await db.SaveChangesAsync(ct);
            return Ok(new { campaign.Id, campaign.Status });
        }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("campaigns/{id:guid}/resume")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();
        try
        {
            campaign.Status = CampaignStatus.Running;
            if (campaign.AgentId.HasValue)
            {
                var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                    x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
                if (agent is not null)
                {
                    agent.Status = AutonomousAgentStatus.Active;
                    agent.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
            await db.SaveChangesAsync(ct);
            return Ok(new { campaign.Id, campaign.Status });
        }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPut("campaigns/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateCampaign(Guid id, CampaignInput input, CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();
        if (campaign.Status is CampaignStatus.Completed) return Conflict(new { detail = "Completed campaigns cannot be edited." });
        if (input.Steps is null || input.Steps.Length == 0) return BadRequest(new { detail = "At least one campaign message is required." });
        if (!await db.TargetLists.AnyAsync(x => x.TenantId == tenantId && x.Id == input.TargetListId, ct))
            return BadRequest(new { detail = "The selected target list does not belong to this tenant." });

        campaign.TargetListId=input.TargetListId; campaign.Name=input.Name.Trim();
        campaign.Goal=input.Goal.Trim(); campaign.SenderName=input.SenderName.Trim(); campaign.SenderEmail=input.SenderEmail.Trim();
        campaign.StartsAtUtc=input.StartsAtUtc;

        var existing=await db.CampaignSteps.Where(x => x.TenantId==tenantId&&x.CampaignId==id).ToListAsync(ct);
        db.CampaignSteps.RemoveRange(existing);
        db.CampaignSteps.AddRange(input.Steps.OrderBy(x=>x.StepNumber).Select(x=>new CampaignStep {
            TenantId=tenantId,CampaignId=id,StepNumber=x.StepNumber,DelayHours=x.DelayHours,Channel=x.Channel,
            SubjectTemplate=x.SubjectTemplate,BodyTemplate=x.BodyTemplate,TemplateId=x.TemplateId
        }));
        await db.SaveChangesAsync(ct);
        return await CampaignDetail(id, ct);
    }

    [HttpGet("campaigns/{id:guid}/activity")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> CampaignActivity(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        if(!await db.Campaigns.AnyAsync(x => x.TenantId==tenantId&&x.Id==id, ct)) return NotFound();
        var messageRows = await db.OutreachMessages.AsNoTracking().Where(x => x.TenantId==tenantId&&x.CampaignId==id)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(100)
            .Select(x => new { x.Id, x.UpdatedAtUtc, x.Status, x.Subject, x.ProviderMessageId }).ToListAsync(ct);
        var messages = messageRows.Select(x => new CampaignActivityItem(x.Id, x.UpdatedAtUtc, "message", x.Status.ToString(), x.Subject, x.ProviderMessageId));
        var replies = await db.ProspectReplies.AsNoTracking().Where(x => x.TenantId==tenantId&&x.CampaignId==id)
            .OrderByDescending(x => x.ReceivedAtUtc).Take(100)
            .Select(x => new CampaignActivityItem(x.Id, x.ReceivedAtUtc, "reply", x.Classification, "Prospect reply", x.Body)).ToListAsync(ct);
        return Ok(messages.Concat(replies).OrderByDescending(x => x.AtUtc).Take(100));
    }

    [HttpGet("messages")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Messages([FromQuery] OutreachStatus? status, CancellationToken ct)
    {
        var tenantId = TenantId;
        var query = from message in db.OutreachMessages.AsNoTracking()
                    join prospect in db.Prospects.AsNoTracking() on message.ProspectId equals prospect.Id
                    join campaign in db.Campaigns.AsNoTracking() on message.CampaignId equals campaign.Id
                    where message.TenantId==tenantId&&(!status.HasValue||message.Status==status.Value)
                    orderby message.CreatedAtUtc descending
                    select new
                    {
                        message.Id,
                        message.CampaignId,
                        campaign = campaign.Name,
                        message.ProspectId,
                        prospect = prospect.CompanyName,
                        prospect.ContactName,
                        prospect.Email,
                        message.Subject,
                        message.Body,
                        message.Status,
                        message.ProviderMessageId,
                        message.SentAtUtc,
                        message.CreatedAtUtc,
                        approvalRequested = db.CrmTasks.Any(task => task.TenantId==tenantId&&task.Title=="APPROVAL: Send outreach "+message.Id&&!task.Completed)
                    };
        return Ok(await query.Take(200).ToListAsync(ct));
    }

    [HttpPost("campaigns/{id:guid}/start")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();

        campaign.Start();

        if (campaign.AgentId.HasValue)
        {
            var agent = await db.AutonomousAcquisitionAgents
                .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Active;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { campaign.Id, campaign.Status, execution = "campaign-runtime" });
    }

    [HttpPost("campaigns/{id:guid}/stop")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();

        campaign.Status = CampaignStatus.Stopped;

        if (campaign.AgentId.HasValue)
        {
            var agent = await db.AutonomousAcquisitionAgents
                .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Stopped;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { campaign.Id, campaign.Status });
    }

    [HttpPost("messages/{id:guid}/delivered")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Delivered(Guid id, DeliveryConfirmation input, CancellationToken ct) =>
        await executor.ConfirmDeliveryAsync(TenantId, id, input.ProviderMessageId, ct) ? Ok() : NotFound();

    [HttpPost("replies")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Reply(ReplyInput input, CancellationToken ct)
    {
        try
        {
            var result = await replyProcessor.ProcessAsync(TenantId, new ProcessProspectReplyRequest(
                input.CampaignId, input.ProspectId, input.OutreachMessageId, input.Body,
                input.Classification, input.SentimentScore, input.RequiresHuman), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch(InvalidOperationException exception)
        {
            return BadRequest(new { detail = exception.Message });
        }
    }

    private static List<OutreachTemplate> CreateDefaultTemplates(Guid tenantId) =>
    [
        new OutreachTemplate { TenantId = tenantId, Name = "Logistics operational benchmark", Description = "Message 1 — initial outreach", SubjectTemplate = "{{company}}: reduce dispatch and delivery exceptions", BodyTemplate = "Hi {{contact}}, I noticed current growth signals at {{company}}. We help {{industry}} teams automate dispatch, warehouse and customer operations. Would a 25-minute operational demo be useful?" },
        new OutreachTemplate { TenantId = tenantId, Name = "Operational benchmark follow-up", Description = "Message 2 — follow-up", SubjectTemplate = "Operational benchmark for {{company}}", BodyTemplate = "Hi {{contact}}, I prepared a short benchmark for teams operating across {{country}}. I can tailor the demo to your fleet, warehouse and delivery workflow." },
        new OutreachTemplate { TenantId = tenantId, Name = "Close the loop", Description = "Message 3 — final follow-up", SubjectTemplate = "Should I close the loop on {{company}}?", BodyTemplate = "Hi {{contact}}, I don't want to keep filling your inbox if this isn't a priority. If improving dispatch, warehouse or delivery operations is on your roadmap, I'm happy to send a short example. Otherwise, I'll close the loop here." }
    ];

    private static void MergeImportedProspect(Prospect prospect, ProspectImportRow row, string domain, string email, string batchSource, DateTime now)
    {
        prospect.CompanyName=Prefer(row.CompanyName, prospect.CompanyName);
        prospect.Domain=Prefer(domain, prospect.Domain);
        prospect.ContactName=Prefer(row.ContactName, prospect.ContactName);
        prospect.Email=Prefer(email, prospect.Email);
        prospect.JobTitle=Prefer(row.JobTitle, prospect.JobTitle);
        prospect.Industry=Prefer(row.Industry, prospect.Industry);
        prospect.Country=Prefer(row.Country, prospect.Country);
        prospect.Source=Prefer(row.Source, Prefer(batchSource, prospect.Source));
        prospect.Priority=Prefer(row.Priority, prospect.Priority);
        prospect.ContactReadiness=Prefer(row.ContactReadiness, prospect.ContactReadiness);
        prospect.SuggestedBuyer=Prefer(row.SuggestedBuyer, prospect.SuggestedBuyer);
        prospect.SizeBand=Prefer(row.SizeBand, prospect.SizeBand);
        prospect.PainHypothesis=Prefer(row.PainHypothesis, prospect.PainHypothesis);
        prospect.Offer=Prefer(row.Offer, prospect.Offer);
        prospect.SourceUrl=Prefer(row.SourceUrl, prospect.SourceUrl);
        prospect.VerificationStatus=Prefer(row.VerificationStatus, prospect.VerificationStatus);
        prospect.OutreachStatus=Prefer(row.OutreachStatus, prospect.OutreachStatus);
        prospect.DatasetOrigin=Prefer(row.DatasetOrigin, prospect.DatasetOrigin);
        prospect.Evaluate(row.FitScore, row.IntentScore);
        prospect.UpdatedAtUtc=now;
    }

    private static string Prefer(string? incoming, string? fallback)
        => string.IsNullOrWhiteSpace(incoming) ? fallback?.Trim()??string.Empty : incoming.Trim();

    private static string NormalizeDomain(string? value)
    {
        var domain = (value??string.Empty).Trim().ToLowerInvariant();
        domain=domain.Replace("https://", string.Empty).Replace("http://", string.Empty);
        if(domain.StartsWith("www.")) domain=domain[4..];
        return domain.Split('/')[0].TrimEnd('.');
    }

    private static string NormalizeEmail(string? value) => (value??string.Empty).Trim().ToLowerInvariant();

    private static CampaignStepRules ParseRules(string json)
    {
        try { return JsonSerializer.Deserialize<CampaignStepRules>(json) ?? new CampaignStepRules(); }
        catch { return new CampaignStepRules(); }
    }

    private static bool Matches(Prospect p, CampaignStepRules r)
    {
        if (r.Qualification.Equals("qualified", StringComparison.OrdinalIgnoreCase) && p.Status != ProspectStatus.Qualified) return false;
        if (p.PriorityScore < Math.Clamp(r.MinimumScore, 0, 100)) return false;
        if (!string.IsNullOrWhiteSpace(r.Industry) && !ContainsAny(p.Industry, r.Industry)) return false;
        if (!string.IsNullOrWhiteSpace(r.Countries) && !ContainsAny(p.Country, r.Countries)) return false;
        if (!string.IsNullOrWhiteSpace(r.ContactRoles) && !ContainsAny(p.JobTitle, r.ContactRoles)) return false;
        return true;
    }

    private static bool ContainsAny(string value, string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));

}

public sealed record TargetListInput(string Name, string Description, Guid? IcpProfileId, bool Dynamic);
public sealed record DiscoveryRequest(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true,
    string? CountriesCsv = null);
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
public sealed record CampaignStepInput(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate, Guid? TemplateId = null, string Qualification = "qualified", int MinimumScore = 70, string Industry = "", string Countries = "", int? CompanySizeMin = null, int? CompanySizeMax = null, string ContactRoles = "", bool StopOnReply = true);
public sealed record OutreachTemplateInput(string Name, string? Description, string SubjectTemplate, string BodyTemplate);
public sealed record CampaignInput(Guid TargetListId, Guid? OfferId, string Name, string Goal, string SenderName, string SenderEmail, DateTime? StartsAtUtc, CampaignStepInput[] Steps);
internal sealed record CampaignStepRules(string Qualification = "qualified", int MinimumScore = 70, string Industry = "", string Countries = "", int? CompanySizeMin = null, int? CompanySizeMax = null, string ContactRoles = "", bool StopOnReply = true);

public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record CampaignActivityItem(Guid Id, DateTime AtUtc, string Type, string Status, string Title, string Detail);