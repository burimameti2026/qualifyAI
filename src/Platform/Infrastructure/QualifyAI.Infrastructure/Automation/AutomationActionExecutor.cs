using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Infrastructure.Email;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Automation;

public sealed record AutomationExecutionResult(bool Success, string LogJson, string? Error = null);

public sealed class AutomationActionExecutor(AppDbContext db, ProspectDiscoveryService discovery, EmailDeliveryService emailDelivery)
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
            foreach(var action in document.RootElement.EnumerateArray())
            {
                step++;
                var type = Read(action, "type", Read(action, "action", string.Empty));
                if(string.IsNullOrWhiteSpace(type))
                    throw new InvalidOperationException($"Automation action {step} has no type.");

                var outcome = await ExecuteActionAsync(rule.TenantId, type, action, run, cancellationToken);
                logs.Add(new { step, type, status = outcome.Status, message = outcome.Message, atUtc = DateTime.UtcNow });
                if(outcome.Status=="blocked") blockedReason??=outcome.Message;
            }

            db.UsageRecords.Add(new UsageRecord
            {
                TenantId=rule.TenantId,
                Meter="automation_actions",
                Quantity=step,
                ReferenceId=run.Id.ToString()
            });
            await db.SaveChangesAsync(cancellationToken);
            return blockedReason is null
                ? new AutomationExecutionResult(true, JsonSerializer.Serialize(logs))
                : new AutomationExecutionResult(false, JsonSerializer.Serialize(logs), blockedReason);
        }
        catch(Exception exception)
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
        switch(type.Trim().ToLowerInvariant())
        {
            case "notifysales":
            case "notify":
                db.Notifications.Add(new Notification
                {
                    TenantId=tenantId,
                    Title=Read(action, "title", "Automation requires attention"),
                    Body=Read(action, "message", $"Automation run {run.Id} completed an action.")
                });
                return ("completed", "Sales notification created.");

            case "createtask":
                db.CrmTasks.Add(new CrmTask
                {
                    TenantId=tenantId,
                    Title=Read(action, "title", "Follow up automation result"),
                    DueAtUtc=DateTime.UtcNow.AddHours(ReadInt(action, "dueInHours", 24))
                });
                return ("completed", "CRM task created.");

            case "requestapproval":
                db.CrmTasks.Add(new CrmTask
                {
                    TenantId=tenantId,
                    Title="APPROVAL: "+Read(action, "title", "Review automated action"),
                    DueAtUtc=DateTime.UtcNow.AddHours(ReadInt(action, "dueInHours", 4))
                });
                db.Notifications.Add(new Notification
                {
                    TenantId=tenantId,
                    Title="Human approval required",
                    Body=Read(action, "message", $"Review automation run {run.Id} before the process continues.")
                });
                return ("waiting-approval", "Approval work item created.");

            case "createopportunity":
                return await CreateOpportunityAsync(tenantId, run, cancellationToken);

            case "discoverprospects":
                var icpId = ReadGuid(action, "icpId");
                if(!icpId.HasValue) return ("blocked", "Online discovery requires an icpId in the automation action.");
                var result = await discovery.DiscoverAsync(tenantId, icpId.Value, new DiscoveryRunOptions(
                    Read(action, "source", "serpapi"),
                    Read(action, "region", string.Empty),
                    ReadInt(action, "maximumResults", 50),
                    ReadInt(action, "minimumScore", 70),
                    Read(action, "targetListName", string.Empty),
                    ReadBool(action, "createTargetList", true)), cancellationToken);
                return ("completed", $"Online discovery found {result.Received} companies; {result.Qualified} qualified, {result.Created} new, {result.Updated} refreshed. Review list: {result.TargetListName??"not created"}.");

            case "enrichprospects":
                var enriched = await db.Prospects.CountAsync(x => x.TenantId==tenantId&&x.ContactName!=""&&x.Email!="", cancellationToken);
                return ("completed", $"{enriched} prospects have decision-maker and contact enrichment.");

            case "scoreprospects":
                var scored = await db.Prospects.CountAsync(x => x.TenantId==tenantId&&x.LastEvaluatedAtUtc!=null, cancellationToken);
                return ("completed", $"{scored} prospects have current fit and intent scores.");

            case "createtargetlist":
                var members = await db.TargetListMembers.CountAsync(x => x.TenantId==tenantId, cancellationToken);
                return ("completed", $"Target audiences contain {members} persisted members.");

            case "wait":
            case "delay":
                return ("scheduled", $"Next action delay: {ReadInt(action, "hours", 1)} hour(s).");

            case "sendemail":
                var messageId = ReadGuid(action, "messageId")??ReadGuid(run.TriggerDataJson, "messageId");
                if(!messageId.HasValue) return ("blocked", "Email action requires a messageId from the action or trigger payload.");
                var delivery = await emailDelivery.SendApprovedAsync(tenantId, messageId.Value, cancellationToken);
                return delivery.Success ? ("completed", $"Email accepted by provider as {delivery.ProviderMessageId}.") : ("blocked", delivery.Error??"Email delivery failed.");

            case "bookmeeting":
                return ("blocked", "Meeting action requires an active calendar provider connection.");

            case "synccrm":
                return ("blocked", "CRM action requires an active CRM provider connection.");

            default:
                throw new InvalidOperationException($"Unsupported automation action '{type}'.");
        }
    }

    private async Task<(string Status, string Message)> CreateOpportunityAsync(Guid tenantId, AutomationRun run, CancellationToken ct)
    {
        using var trigger = JsonDocument.Parse(run.TriggerDataJson);
        if(!trigger.RootElement.TryGetProperty("leadId", out var value)||!Guid.TryParse(value.GetString(), out var leadId))
            return ("skipped", "No leadId was supplied by the trigger.");

        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId==tenantId&&x.Id==leadId, ct);
        if(lead is null) return ("skipped", "Trigger lead no longer exists.");
        if(await db.Opportunitys.AnyAsync(x => x.TenantId==tenantId&&x.LeadId==leadId, ct))
            return ("idempotent", "Opportunity already exists for this lead.");

        var defaultPipelineId = await db.Pipelines.Where(x => x.TenantId==tenantId&&x.IsDefault)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var firstStage = defaultPipelineId.HasValue
            ? await db.PipelineStages.Where(x => x.TenantId==tenantId&&x.PipelineId==defaultPipelineId.Value).OrderBy(x => x.SortOrder).FirstOrDefaultAsync(ct)
            : null;

        db.Opportunitys.Add(new Opportunity
        {
            TenantId=tenantId,
            LeadId=lead.Id,
            CompanyId=lead.CompanyId,
            ContactId=lead.ContactId,
            Name=$"Qualified opportunity {lead.Id.ToString()[..8]}",
            Amount=lead.EstimatedValue??0m,
            PipelineStageId=firstStage?.Id,
            ExpectedCloseUtc=DateTime.UtcNow.AddDays(30)
        });
        return ("completed", "Opportunity created.");
    }

    private static string Read(JsonElement element, string name, string fallback) =>
        element.TryGetProperty(name, out var value)&&value.ValueKind==JsonValueKind.String
            ? value.GetString()??fallback
            : fallback;

    private static int ReadInt(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value)&&value.TryGetInt32(out var parsed) ? parsed : fallback;

    private static Guid? ReadGuid(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)&&Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;

    private static Guid? ReadGuid(string json, string name)
    {
        try { using var document = JsonDocument.Parse(json); return ReadGuid(document.RootElement, name); }
        catch { return null; }
    }

    private static bool ReadBool(JsonElement element, string name, bool fallback) =>
        element.TryGetProperty(name, out var value)&&value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
}