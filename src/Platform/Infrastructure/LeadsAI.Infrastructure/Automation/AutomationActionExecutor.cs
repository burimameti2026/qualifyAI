using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Infrastructure.Email;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Automation;

public sealed record AutomationExecutionResult(bool Success, string LogJson, string? Error = null);

public sealed class AutomationActionExecutor(
    AppDbContext db,
    ProspectDiscoveryService discovery,
    EmailDeliveryService emailDelivery,
    IAutonomousAcquisitionBackendService acquisitionBackend,
    IAutonomousAcquisitionTemplateRegistry templates)
{
    public async Task<AutomationExecutionResult> ExecuteAsync(
        AutomationRule rule,
        AutomationRun run,
        CancellationToken cancellationToken = default)
    {
        var logs = new List<object>();
        try
        {
            using var document = JsonDocument.Parse(rule.ActionsJson);
            var step = 0;
            string? blockedReason = null;

            foreach (var action in document.RootElement.EnumerateArray())
            {
                step++;
                var rawType = Read(action, "type", Read(action, "action", string.Empty));
                if (string.IsNullOrWhiteSpace(rawType))
                    throw new InvalidOperationException($"Automation action {step} has no type.");

                var type = NormalizeAction(rawType);
                var outcome = await ExecuteActionAsync(rule.TenantId, type, action, run, cancellationToken);
                logs.Add(new { step, type, status = outcome.Status, message = outcome.Message, atUtc = DateTime.UtcNow });
                if (outcome.Status == "blocked")
                    blockedReason ??= outcome.Message;
            }

            db.UsageRecords.Add(new UsageRecord
            {
                TenantId = rule.TenantId,
                Meter = "automation_actions",
                Quantity = step,
                ReferenceId = run.Id.ToString()
            });
            await db.SaveChangesAsync(cancellationToken);

            return blockedReason is null
                ? new AutomationExecutionResult(true, JsonSerializer.Serialize(logs))
                : new AutomationExecutionResult(false, JsonSerializer.Serialize(logs), blockedReason);
        }
        catch (Exception exception)
        {
            logs.Add(new { status = "failed", message = exception.Message, atUtc = DateTime.UtcNow });
            return new AutomationExecutionResult(false, JsonSerializer.Serialize(logs), exception.Message);
        }
    }

    private async Task<(string Status, string Message)> ExecuteActionAsync(
        Guid tenantId,
        string type,
        JsonElement action,
        AutomationRun run,
        CancellationToken cancellationToken)
    {
        switch (type)
        {
            case "notify":
                db.Notifications.Add(new Notification
                {
                    TenantId = tenantId,
                    Title = Read(action, "title", "Automation requires attention"),
                    Body = Read(action, "message", $"Automation run {run.Id} completed an action.")
                });
                return ("completed", "Sales notification created.");

            case "create_task":
                db.CrmTasks.Add(new CrmTask
                {
                    TenantId = tenantId,
                    Title = Read(action, "title", "Follow up automation result"),
                    DueAtUtc = DateTime.UtcNow.AddHours(ReadInt(action, "dueInHours", 24))
                });
                return ("completed", "CRM task created.");

            case "request_approval":
                db.CrmTasks.Add(new CrmTask
                {
                    TenantId = tenantId,
                    Title = "APPROVAL: " + Read(action, "title", "Review automated action"),
                    DueAtUtc = DateTime.UtcNow.AddHours(ReadInt(action, "dueInHours", 4))
                });
                db.Notifications.Add(new Notification
                {
                    TenantId = tenantId,
                    Title = "Human approval required",
                    Body = Read(action, "message", $"Review automation run {run.Id} before the process continues.")
                });
                return ("waiting-approval", "Approval work item created.");

            case "create_opportunity":
                return await CreateOpportunityAsync(tenantId, run, cancellationToken);

            case "discover_prospects":
                var icpId = ReadGuid(action, "icpId");
                if (!icpId.HasValue)
                    return ("blocked", "Online discovery requires an icpId in the automation action.");

                var result = await discovery.DiscoverAsync(tenantId, icpId.Value, new DiscoveryRunOptions(
                    Read(action, "source", "serpapi"),
                    Read(action, "region", string.Empty),
                    ReadInt(action, "maximumResults", 50),
                    ReadInt(action, "minimumScore", 70),
                    Read(action, "targetListName", string.Empty),
                    ReadBool(action, "createTargetList", true)), cancellationToken);
                return ("completed", $"Online discovery found {result.Received} companies; {result.Qualified} qualified, {result.Created} new, {result.Updated} refreshed. Review list: {result.TargetListName ?? "not created"}.");

            case "deduplicate":
                return await DeduplicateAsync(tenantId, cancellationToken);

            case "enrich_company":
            case "enrich_contact":
            case "qualify":
            case "score":
                return await ResearchProspectsAsync(tenantId, action, cancellationToken);

            case "personalize":
                return await PersonalizeProspectAsync(tenantId, action, cancellationToken);

            case "add_to_target_list":
                var members = await db.TargetListMembers.CountAsync(x => x.TenantId == tenantId, cancellationToken);
                return ("completed", $"Target audiences contain {members} persisted members; discovery/qualification owns membership creation.");

            case "add_to_campaign":
                return ("completed", "Campaign enrollment is handled by the autonomous acquisition orchestrator after qualification, preserving campaign and tenant safeguards.");

            case "send_outreach":
                return await SendOutreachAsync(tenantId, action, run, cancellationToken);

            case "process_reply":
                return ("completed", "Reply processing is handled by the acquisition reply processor and feeds qualification/CRM state.");

            case "sync_crm":
                return ("blocked", "CRM action requires an active CRM provider connection.");

            case "book_meeting":
                return ("blocked", "Meeting action requires an active calendar provider connection.");

            case "wait":
                return ("scheduled", $"Workflow requested a {Math.Max(1, ReadInt(action, "hours", 1))}-hour delay; run scheduling/resume is required before execution can continue.");

            case "send_email":
                var messageId = ReadGuid(action, "messageId") ?? ReadGuid(run.TriggerDataJson, "messageId");
                if (!messageId.HasValue)
                    return ("blocked", "Email action requires a messageId from the action or trigger payload.");
                var delivery = await emailDelivery.SendApprovedAsync(tenantId, messageId.Value, cancellationToken);
                return delivery.Success
                    ? ("completed", $"Email accepted by provider as {delivery.ProviderMessageId}.")
                    : ("blocked", delivery.Error ?? "Email delivery failed.");

            default:
                throw new InvalidOperationException($"Unsupported automation action '{type}'.");
        }
    }

    private async Task<(string Status, string Message)> ResearchProspectsAsync(Guid tenantId, JsonElement action, CancellationToken ct)
    {
        var threshold = Math.Clamp(ReadInt(action, "minimumScore", 70), 0, 100);
        var requestedId = ReadGuid(action, "prospectId");
        var query = db.Prospects.Where(x => x.TenantId == tenantId && x.Status != ProspectStatus.Suppressed);
        var prospects = requestedId.HasValue
            ? await query.Where(x => x.Id == requestedId.Value).Take(1).ToListAsync(ct)
            : await query.OrderByDescending(x => x.PriorityScore).Take(Math.Clamp(ReadInt(action, "maximumResults", 100), 1, 500)).ToListAsync(ct);

        if (prospects.Count == 0)
            return ("skipped", "No eligible prospects were available for enrichment and qualification.");

        var qualified = 0;
        var ready = 0;
        foreach (var prospect in prospects)
        {
            var research = await acquisitionBackend.ResearchAsync(tenantId, Guid.Empty, prospect, threshold, ct);
            if (research.Qualified) qualified++;
            if (research.ContactReadiness == "ready") ready++;
        }

        return ("completed", $"Processed {prospects.Count} prospect(s): {qualified} qualified and {ready} contact-ready.");
    }

    private async Task<(string Status, string Message)> PersonalizeProspectAsync(Guid tenantId, JsonElement action, CancellationToken ct)
    {
        var prospectId = ReadGuid(action, "prospectId");
        if (!prospectId.HasValue)
            return ("blocked", "Personalization requires a prospectId so generated outreach is deterministic and auditable.");

        var prospect = await db.Prospects.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == prospectId.Value, ct);
        if (prospect is null)
            return ("skipped", "Prospect no longer exists in this tenant.");

        var template = templates.Resolve(Read(action, "templateCode", "logistics"));
        var generated = await acquisitionBackend.GenerateOutreachAsync(prospect, template, ReadInt(action, "step", 1), ct);
        return ("completed", $"Personalized outreach generated for {prospect.CompanyName}; {generated.Length} characters prepared for the campaign delivery stage.");
    }

    private async Task<(string Status, string Message)> SendOutreachAsync(Guid tenantId, JsonElement action, AutomationRun run, CancellationToken ct)
    {
        var prospectId = ReadGuid(action, "prospectId") ?? ReadGuid(run.TriggerDataJson, "prospectId");
        if (!prospectId.HasValue)
            return ("blocked", "Outreach requires a prospectId from the action or trigger payload.");

        var prospect = await db.Prospects.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == prospectId.Value, ct);
        if (prospect is null)
            return ("skipped", "Prospect no longer exists in this tenant.");

        var dailyLimit = Math.Max(1, ReadInt(action, "dailyLimit", 50));
        if (!await acquisitionBackend.CanContactAsync(tenantId, prospect, dailyLimit, ct))
            return ("blocked", "Prospect is suppressed, lacks a deliverable contact, or the daily outreach limit has been reached.");

        var template = templates.Resolve(Read(action, "templateCode", "logistics"));
        var message = await acquisitionBackend.GenerateOutreachAsync(prospect, template, ReadInt(action, "step", 1), ct);
        return ("prepared", $"Outreach prepared for {prospect.CompanyName}; delivery remains gated by an approved campaign message/provider.");
    }

    private async Task<(string Status, string Message)> DeduplicateAsync(Guid tenantId, CancellationToken ct)
    {
        var prospects = await db.Prospects.Where(x => x.TenantId == tenantId && x.Domain != null && x.Domain != "")
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        foreach (var prospect in prospects)
        {
            var key = prospect.Domain.Trim().TrimEnd('/').ToLowerInvariant();
            if (seen.Add(key))
                continue;
            if (prospect.Status != ProspectStatus.Suppressed)
                prospect.Status = ProspectStatus.Suppressed;
            duplicates++;
        }
        await db.SaveChangesAsync(ct);
        return ("completed", $"Deduplication checked {prospects.Count} prospect(s) and suppressed {duplicates} duplicate domain record(s).");
    }

    private async Task<(string Status, string Message)> CreateOpportunityAsync(Guid tenantId, AutomationRun run, CancellationToken ct)
    {
        using var trigger = JsonDocument.Parse(run.TriggerDataJson);
        if (!trigger.RootElement.TryGetProperty("leadId", out var value) || !Guid.TryParse(value.GetString(), out var leadId))
            return ("skipped", "No leadId was supplied by the trigger.");

        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == leadId, ct);
        if (lead is null)
            return ("skipped", "Trigger lead no longer exists.");
        if (await db.Opportunitys.AnyAsync(x => x.TenantId == tenantId && x.LeadId == leadId, ct))
            return ("idempotent", "Opportunity already exists for this lead.");

        var defaultPipelineId = await db.Pipelines.Where(x => x.TenantId == tenantId && x.IsDefault)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var firstStage = defaultPipelineId.HasValue
            ? await db.PipelineStages.Where(x => x.TenantId == tenantId && x.PipelineId == defaultPipelineId.Value).OrderBy(x => x.SortOrder).FirstOrDefaultAsync(ct)
            : null;

        db.Opportunitys.Add(new Opportunity
        {
            TenantId = tenantId,
            LeadId = lead.Id,
            CompanyId = lead.CompanyId,
            ContactId = lead.ContactId,
            Name = $"Qualified opportunity {lead.Id.ToString()[..8]}",
            Amount = lead.EstimatedValue ?? 0m,
            PipelineStageId = firstStage?.Id,
            ExpectedCloseUtc = DateTime.UtcNow.AddDays(30)
        });
        return ("completed", "Opportunity created.");
    }

    private static string NormalizeAction(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);

        return normalized switch
        {
            "notifysales" or "notify" => "notify",
            "createtask" => "create_task",
            "requestapproval" => "request_approval",
            "createopportunity" => "create_opportunity",
            "discoverprospects" or "discover" => "discover_prospects",
            "deduplicate" or "dedup" => "deduplicate",
            "enrichprospects" or "enrich" or "enrichcompany" => "enrich_company",
            "enrichcontact" => "enrich_contact",
            "qualify" or "qualification" => "qualify",
            "score" or "scoreprospects" => "score",
            "personalize" or "personalise" or "aipersonalize" => "personalize",
            "createtargetlist" or "addtotargetlist" => "add_to_target_list",
            "addtocampaign" or "enrollcampaign" => "add_to_campaign",
            "sendoutreach" or "outreach" => "send_outreach",
            "processreply" or "processreplies" => "process_reply",
            "synccrm" => "sync_crm",
            "bookmeeting" => "book_meeting",
            "wait" or "delay" => "wait",
            "sendemail" => "send_email",
            _ => throw new InvalidOperationException($"Unsupported automation action '{value}'.")
        };
    }

    private static string Read(JsonElement element, string name, string fallback) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static int ReadInt(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

    private static Guid? ReadGuid(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;

    private static Guid? ReadGuid(string json, string name)
    {
        try { using var document = JsonDocument.Parse(json); return ReadGuid(document.RootElement, name); }
        catch { return null; }
    }

    private static bool ReadBool(JsonElement element, string name, bool fallback) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
}
