using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Api.Services;

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
    ProspectDiscoveryService discovery,
    IAgentJobFactory jobFactory,
    AcquisitionCriteriaService criteriaService) : ControllerBase
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
    public async Task<IActionResult> Icp(CancellationToken ct)
    {
        var rows = await db.IcpProfiles.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.TenantId,
            x.Name,
            x.Industry,
            x.CountriesCsv,
            x.MinimumEmployees,
            x.MaximumEmployees,
            x.IntentKeywordsCsv,
            x.CriteriaJson,
            x.Active,
            x.LastDiscoveryAtUtc,
            minimumScore = criteriaService.ReadMinimumScore(x.CriteriaJson)
        }));
    }

    [HttpPost("icp")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveIcp([FromBody] IcpSaveRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { error = "ICP name is required." });

        var tenantId = TenantId;
        IcpProfile? profile = null;
        if (input.Id.HasValue && input.Id.Value != Guid.Empty)
            profile = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == input.Id.Value, ct);

        var isNew = profile is null;
        profile ??= new IcpProfile { Id = Guid.NewGuid(), TenantId = tenantId };

        profile.Name = input.Name.Trim();
        profile.Industry = input.Industry?.Trim() ?? string.Empty;
        profile.CountriesCsv = input.CountriesCsv?.Trim() ?? string.Empty;
        profile.IntentKeywordsCsv = input.IntentKeywordsCsv?.Trim() ?? string.Empty;
        profile.MinimumEmployees = input.MinimumEmployees;
        profile.MaximumEmployees = input.MaximumEmployees;
        profile.CriteriaJson = criteriaService.NormalizeCriteria(input.CriteriaJson, input.MinimumScore);
        profile.Active = input.Active;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        if (isNew)
            db.IcpProfiles.Add(profile);

        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            profile.Id,
            profile.TenantId,
            profile.Name,
            profile.Industry,
            profile.CountriesCsv,
            profile.MinimumEmployees,
            profile.MaximumEmployees,
            profile.IntentKeywordsCsv,
            profile.CriteriaJson,
            profile.Active,
            profile.LastDiscoveryAtUtc,
            minimumScore = criteriaService.ReadMinimumScore(profile.CriteriaJson)
        });
    }

    [HttpGet("prospects")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Prospects([FromQuery] int minimumScore = 0, CancellationToken ct = default)
    {
        var threshold = Math.Clamp(minimumScore, 0, 100) * 100;
        var rows = await db.Prospects
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.FitScore * 55 + x.IntentScore * 45 >= threshold)
            .OrderByDescending(x => x.FitScore * 55 + x.IntentScore * 45)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("target-lists")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> TargetLists(CancellationToken ct)
    {
        var rows = await db.TargetLists
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("discovery/providers")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public IActionResult DiscoveryProviders() => Ok(discovery.ProviderStatus());

    [HttpPost("icp/{id:guid}/discover")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
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

}
public sealed record DiscoveryRequest(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true);

public sealed record IcpSaveRequest(
    Guid? Id,
    string Name,
    string? Industry,
    string? CountriesCsv,
    int? MinimumEmployees,
    int? MaximumEmployees,
    string? IntentKeywordsCsv,
    string? CriteriaJson,
    bool Active = true,
    int MinimumScore = 70);

public sealed record CampaignContainerCreateRequest(string? Name, string? PackageCode, string? PackageVersion, string? ConfigurationJson);
public sealed record ContainerTargetListRequest(Guid? TargetListId);
public sealed record CampaignPlanRequest(string PlanJson);
public sealed record CampaignMessagesRequest(IReadOnlyList<CampaignMessageStepRequest> Steps);
public sealed record CampaignMessageStepRequest(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record CampaignActivityItem(Guid Id, DateTime AtUtc, string Type, string Status, string Title, string Detail);
