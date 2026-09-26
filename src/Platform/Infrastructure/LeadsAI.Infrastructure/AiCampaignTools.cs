using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Application;
using LeadsAI.Domain;

namespace LeadsAI.Infrastructure;

public sealed class CreateTargetListTool(AppDbContext db) : IAiTool
{
    public string Name => "CreateTargetList";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var root = doc.RootElement;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) return new(false, "{}", "name is required.");

        Guid? icpId = null;
        if (root.TryGetProperty("icpId", out var i) && Guid.TryParse(i.GetString(), out var parsed)) icpId = parsed;
        if (icpId.HasValue && !await db.IcpProfiles.AnyAsync(x => x.TenantId == context.TenantId && x.Id == icpId.Value && x.Active, ct))
            return new(false, "{}", "icpId is invalid or inactive for this tenant.");

        var list = new TargetList
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Name = name.Trim(),
            Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            IcpProfileId = icpId,
            Dynamic = root.TryGetProperty("dynamic", out var dyn) && dyn.ValueKind == JsonValueKind.True
        };
        db.TargetLists.Add(list);

        if (root.TryGetProperty("prospectIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            var prospectIds = ids.EnumerateArray().Select(x => Guid.TryParse(x.GetString(), out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty).Distinct().ToArray();
            var valid = await db.Prospects.Where(x => x.TenantId == context.TenantId && prospectIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
            db.TargetListMembers.AddRange(valid.Select(id => new TargetListMember
            {
                Id = Guid.NewGuid(), TenantId = context.TenantId, TargetListId = list.Id, ProspectId = id, AddedAtUtc = DateTime.UtcNow
            }));
        }

        await db.SaveChangesAsync(ct);
        var count = await db.TargetListMembers.CountAsync(x => x.TenantId == context.TenantId && x.TargetListId == list.Id, ct);
        return new(true, JsonSerializer.Serialize(new { list.Id, list.Name, list.IcpProfileId, members = count }));
    }
}

public sealed class RunAutonomousAcquisitionTool(AppDbContext db) : IAiTool
{
    public string Name => "RunAutonomousAcquisition";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson);
        var root = doc.RootElement;
        var agentText = root.TryGetProperty("agentId", out var a) ? a.GetString() : null;
        var campaignText = root.TryGetProperty("campaignId", out var c) ? c.GetString() : null;
        if (!Guid.TryParse(campaignText, out var campaignId))
            return new(false, "{}", "campaignId is required.");

        var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == context.TenantId && x.Id == campaignId, ct);
        if (campaign is null) return new(false, "{}", "campaignId does not belong to this tenant.");
        if (!campaign.AgentId.HasValue) return new(false, "{}", "The campaign has no autonomous acquisition agent configured.");
        var agentId = campaign.AgentId.Value;
        var agent = await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(x => x.TenantId == context.TenantId && x.Id == agentId, ct);
        if (agent is null) return new(false, "{}", "The campaign agent does not belong to this tenant.");
        if (campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped or CampaignStatus.Completed)
            return new(false, "{}", "The campaign is not runnable in its current status.");
        if (agent.Status is AutonomousAgentStatus.Stopped)
            return new(false, "{}", "The autonomous acquisition agent is stopped.");

        var existing = await db.AutonomousAcquisitionAgentRuns
            .AnyAsync(x => x.TenantId == context.TenantId && x.AgentId == agentId && x.CampaignId == campaignId &&
                           x.Status is AutonomousAgentRunStatus.Queued or AutonomousAgentRunStatus.Running or AutonomousAgentRunStatus.WaitingApproval, ct);
        if (existing) return new(false, "{}", "This agent already has a queued or running acquisition run.");

        var run = new AutonomousAcquisitionAgentRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            AgentId = agentId,
            CampaignId = campaignId,
            IsManual = true,
            Status = AutonomousAgentRunStatus.Queued,
            ScheduledAtUtc = DateTime.UtcNow
        };
        db.AutonomousAcquisitionAgentRuns.Add(run);
        await db.SaveChangesAsync(ct);

        return new(true, JsonSerializer.Serialize(new
        {
            run.Id,
            run.AgentId,
            run.CampaignId,
            run.Status,
            run.ScheduledAtUtc,
            message = "Acquisition run queued. The worker will execute discovery and qualification."
        }));
    }
}

public sealed class CreateCampaignTool(AppDbContext db) : IAiTool
{
    public string Name => "CreateCampaign";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var root = doc.RootElement;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        var targetListText = root.TryGetProperty("targetListId", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(name) || !Guid.TryParse(targetListText, out var targetListId))
            return new(false, "{}", "name and a valid targetListId are required.");

        var targetList = await db.TargetLists.FirstOrDefaultAsync(x => x.TenantId == context.TenantId && x.Id == targetListId, ct);
        if (targetList is null) return new(false, "{}", "targetListId does not belong to this tenant.");

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            TargetListId = targetListId,
            Name = name.Trim(),
            Goal = Get(root, "goal", "book-demo"),
            Objective = Get(root, "objective", ""),
            SenderName = Get(root, "senderName", ""),
            SenderEmail = Get(root, "senderEmail", ""),
            StartsAtUtc = GetDate(root, "startsAtUtc")
        };
        db.Campaigns.Add(campaign);

        var steps = root.TryGetProperty("steps", out var stepsJson) && stepsJson.ValueKind == JsonValueKind.Array
            ? stepsJson.EnumerateArray().Select((x, index) => new CampaignStep
            {
                Id = Guid.NewGuid(), TenantId = context.TenantId, CampaignId = campaign.Id,
                StepNumber = GetInt(x, "stepNumber", index + 1),
                DelayHours = GetInt(x, "delayHours", index == 0 ? 0 : 48),
                Channel = Get(x, "channel", "email"),
                SubjectTemplate = Get(x, "subjectTemplate", ""),
                BodyTemplate = Get(x, "bodyTemplate", "")
            }).OrderBy(x => x.StepNumber).ToArray()
            : Array.Empty<CampaignStep>();

        if (steps.Length > 0) db.CampaignSteps.AddRange(steps);
        await db.SaveChangesAsync(ct);
        return new(true, JsonSerializer.Serialize(new { campaign.Id, campaign.Name, campaign.TargetListId, campaign.Goal, campaign.Objective, steps = steps.Length }));
    }

    private static string Get(JsonElement r, string p, string fallback) => r.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static int GetInt(JsonElement r, string p, int fallback) => r.TryGetProperty(p, out var v) && v.TryGetInt32(out var i) ? Math.Max(0, i) : fallback;
    private static DateTime? GetDate(JsonElement r, string p) => r.TryGetProperty(p, out var v) && DateTime.TryParse(v.GetString(), out var d) ? d : null;
}
