using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed record ProcessProspectReplyRequest(
    Guid CampaignId,
    Guid ProspectId,
    Guid? OutreachMessageId,
    string? Body,
    string? Classification,
    int SentimentScore = 0,
    bool RequiresHuman = false);

public sealed record ProcessProspectReplyResult(
    Guid ReplyId,
    string Classification,
    bool Interested,
    bool Suppressed,
    Guid ProspectId,
    Guid? CompanyId,
    Guid? ContactId,
    Guid? LeadId,
    Guid? OpportunityId,
    string NextAction);

/// <summary>
/// Applies every prospect reply through one tenant-safe path. It stops future campaign
/// sends immediately, suppresses opt-outs, and converts genuine interest into CRM work.
/// </summary>
public sealed class ProspectReplyProcessingService(AppDbContext db)
{
    public async Task<ProcessProspectReplyResult?> ProcessAsync(
        Guid tenantId,
        ProcessProspectReplyRequest input,
        CancellationToken ct = default)
    {
        var recipient = await db.CampaignRecipients.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.CampaignId == input.CampaignId && x.ProspectId == input.ProspectId, ct);
        var prospect = await db.Prospects.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == input.ProspectId, ct);
        if (recipient is null || prospect is null)
            return null;

        if (input.OutreachMessageId.HasValue && !await db.OutreachMessages.AnyAsync(
                x => x.TenantId == tenantId && x.Id == input.OutreachMessageId &&
                     x.CampaignId == input.CampaignId && x.ProspectId == input.ProspectId, ct))
            throw new InvalidOperationException("The reply does not match the selected outreach message.");

        var classification = NormalizeClassification(input.Classification, input.Body);
        var interested = classification is "interested" or "positive" or "demo-request" or "meeting-request";
        var suppressed = classification is "unsubscribe" or "not-interested" or "spam";

        // Every reply, including an out-of-office response, pauses the sequence until a
        // person reviews it. This prevents any queued follow-up from leaving afterwards.
        recipient.Status = suppressed ? "suppressed" : "replied";
        recipient.RepliedAtUtc = DateTime.UtcNow;
        recipient.NextRunAtUtc = null;
        prospect.Status = suppressed ? ProspectStatus.Suppressed : interested ? ProspectStatus.DemoReady : ProspectStatus.Replied;

        var queuedFollowUps = await db.OutreachMessages.Where(x =>
                x.TenantId == tenantId && x.CampaignId == input.CampaignId && x.ProspectId == input.ProspectId &&
                x.Status == OutreachStatus.Queued && (!input.OutreachMessageId.HasValue || x.Id != input.OutreachMessageId.Value))
            .ToListAsync(ct);
        foreach (var followUp in queuedFollowUps)
            followUp.Status = OutreachStatus.Suppressed;

        var reply = new ProspectReply
        {
            TenantId = tenantId,
            CampaignId = input.CampaignId,
            ProspectId = input.ProspectId,
            OutreachMessageId = input.OutreachMessageId,
            Body = input.Body?.Trim() ?? string.Empty,
            Classification = classification,
            SentimentScore = Math.Clamp(input.SentimentScore, -100, 100),
            RequiresHuman = input.RequiresHuman || interested || classification is "unclassified" or "auto-reply"
        };
        db.ProspectReplies.Add(reply);

        if (input.OutreachMessageId.HasValue)
        {
            var message = await db.OutreachMessages.FirstAsync(x => x.TenantId == tenantId && x.Id == input.OutreachMessageId, ct);
            message.Status = suppressed ? OutreachStatus.Suppressed : OutreachStatus.Replied;
        }

        Company? company = null;
        Contact? contact = null;
        Lead? lead = null;
        Opportunity? opportunity = null;
        if (suppressed)
        {
            contact = await EnsureContactAsync(tenantId, prospect, null, ct);
            await SuppressAsync(tenantId, contact, classification, ct);
        }
        else if (interested)
        {
            company = prospect.CompanyId.HasValue
                ? await db.Companys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == prospect.CompanyId, ct)
                : await db.Companys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Domain == prospect.Domain, ct);
            if (company is null)
            {
                company = Company.Create(tenantId, prospect.CompanyName, prospect.Domain, prospect.Industry, null, prospect.Country, null);
                db.Companys.Add(company);
            }

            contact = await EnsureContactAsync(tenantId, prospect, company.Id, ct);
            prospect.CompanyId = company.Id;
            prospect.ContactId = contact.Id;

            lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contact.Id && x.Source == "outreach", ct);
            if (lead is null)
            {
                lead = Lead.Create(tenantId, contact.Id, company.Id, "outreach", Math.Max(80, prospect.PriorityScore), null, $"Interested reply to campaign: {input.Body}".Trim());
                lead.Qualify();
                db.Leads.Add(lead);
            }

            opportunity = await db.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id && x.Status == OpportunityStatus.Open, ct);
            if (opportunity is null)
            {
                var stage = await db.PipelineStages.Where(x => x.TenantId == tenantId).OrderBy(x => x.SortOrder).FirstOrDefaultAsync(ct);
                opportunity = new Opportunity
                {
                    TenantId = tenantId,
                    LeadId = lead.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id,
                    PipelineStageId = stage?.Id,
                    Name = $"{company.Name} - discovery call",
                    Amount = 500m,
                    ExpectedCloseUtc = DateTime.UtcNow.AddDays(21)
                };
                db.Opportunitys.Add(opportunity);
                db.RevenueAttributions.Add(new RevenueAttribution { TenantId = tenantId, LeadId = lead.Id, OpportunityId = opportunity.Id, InfluencedRevenue = opportunity.Amount, Model = "campaign-reply" });
            }

            if (!await db.CrmTasks.AnyAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id && !x.Completed && x.Title == "Book discovery call", ct))
                db.CrmTasks.Add(new CrmTask { TenantId = tenantId, LeadId = lead.Id, ContactId = contact.Id, Title = "Book discovery call", DueAtUtc = DateTime.UtcNow.AddHours(2) });
            db.CrmActivitys.Add(new CrmActivity { TenantId = tenantId, CompanyId = company.Id, ContactId = contact.Id, LeadId = lead.Id, Type = "campaign-reply", Subject = $"Interested reply: {classification}", Body = reply.Body });
        }

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            Action = interested ? "acquisition.reply.converted" : suppressed ? "acquisition.reply.suppressed" : "acquisition.reply.received",
            EntityType = nameof(Prospect),
            EntityId = prospect.Id.ToString(),
            DataJson = JsonSerializer.Serialize(new { reply.Id, classification, opportunityId = opportunity?.Id })
        });
        await db.SaveChangesAsync(ct);

        return new ProcessProspectReplyResult(
            reply.Id, classification, interested, suppressed, prospect.Id, company?.Id, contact?.Id,
            lead?.Id, opportunity?.Id,
            interested ? "book-discovery-call" : suppressed ? "suppressed" : classification == "auto-reply" ? "review-auto-reply" : "review-reply");
    }

    private async Task<Contact> EnsureContactAsync(Guid tenantId, Prospect prospect, Guid? companyId, CancellationToken ct)
    {
        var contact = prospect.ContactId.HasValue
            ? await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == prospect.ContactId, ct)
            : await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == prospect.Email, ct);
        if (contact is null)
        {
            var names = SplitName(prospect.ContactName);
            contact = Contact.Create(tenantId, companyId, names.FirstName, names.LastName, prospect.Email, string.Empty, "outreach");
            db.Contacts.Add(contact);
        }
        else if (companyId.HasValue)
            contact.UpdateProfile(companyId, contact.FirstName, contact.LastName, contact.Email, contact.Phone, "outreach");
        return contact;
    }

    private async Task SuppressAsync(Guid tenantId, Contact contact, string reason, CancellationToken ct)
    {
        var consent = await db.ConsentRecords.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.ContactId == contact.Id && x.Type == "marketing", ct);
        if (consent is null)
        {
            consent = new ConsentRecord { TenantId = tenantId, ContactId = contact.Id, Type = "marketing" };
            db.ConsentRecords.Add(consent);
        }
        consent.Granted = false;
        consent.RecordedAtUtc = DateTime.UtcNow;
        consent.Source = $"reply-{reason}";
    }

    public static string NormalizeClassification(string? supplied, string? body)
    {
        var value = (supplied ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(value) && value != "unclassified")
            return value switch
            {
                "yes" or "positive" or "demo-request" or "meeting-request" => "interested",
                "no" or "negative" or "declined" or "not-interested" => "not-interested",
                "opt-out" or "remove" => "unsubscribe",
                "ooo" or "out-of-office" => "auto-reply",
                _ => value
            };
        var text = (body ?? string.Empty).ToLowerInvariant();
        if (text.Contains("out of office") || text.Contains("automatic reply") || text.Contains("auto-reply")) return "auto-reply";
        if (text.Contains("unsubscribe") || text.Contains("remove me") || text.Contains("opt out")) return "unsubscribe";
        if (text.Contains("not interested") || text.Contains("no thanks") || text.Contains("do not contact")) return "not-interested";
        if (text.Contains("demo") || text.Contains("meeting") || text.Contains("call") || text.Contains("interested")) return "interested";
        return "unclassified";
    }

    private static (string FirstName, string LastName) SplitName(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch { 0 => ("Prospect", string.Empty), 1 => (parts[0], string.Empty), _ => (parts[0], parts[1]) };
    }
}
