using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed class CampaignExecutionService(AppDbContext db)
{
    public async Task<int> QueueDueMessagesAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var recipients = await db.CampaignRecipients
                .Where(x => (!tenantId.HasValue || x.TenantId == tenantId.Value) &&
                            x.Status == "active" && x.NextRunAtUtc <= now)
                .OrderBy(x => x.NextRunAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            var queued = 0;
            foreach (var recipient in recipients)
            {
                // Atomically claim the recipient so concurrent worker instances cannot
                // create duplicate outreach messages for the same campaign step.
                var claimed = await db.CampaignRecipients
                    .Where(x => x.TenantId == recipient.TenantId &&
                                x.Id == recipient.Id &&
                                x.Status == "active" &&
                                x.NextRunAtUtc <= now)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, "processing"), cancellationToken);

                if (claimed == 0)
                    continue;

                var campaign = await db.Campaigns.FirstOrDefaultAsync(
                    x => x.Id == recipient.CampaignId && x.TenantId == recipient.TenantId && x.Status == CampaignStatus.Running,
                    cancellationToken);
                if (campaign is null)
                {
                    recipient.Status = "active";
                    continue;
                }

                var prospect = await db.Prospects.FirstOrDefaultAsync(
                    x => x.Id == recipient.ProspectId && x.TenantId == recipient.TenantId,
                    cancellationToken);
                if (prospect is null || prospect.Status == ProspectStatus.Suppressed)
                {
                    recipient.Status = "suppressed";
                    recipient.NextRunAtUtc = null;
                    continue;
                }

                if (prospect.Status != ProspectStatus.Qualified ||
                    string.IsNullOrWhiteSpace(prospect.Email) ||
                    prospect.Email.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
                {
                    recipient.Status = prospect.Status == ProspectStatus.Suppressed ? "suppressed" : "not-ready";
                    recipient.NextRunAtUtc = null;
                    continue;
                }

                var steps = await db.CampaignSteps
                    .Where(x => x.TenantId == recipient.TenantId &&
                                x.CampaignId == campaign.Id &&
                                x.StepNumber > recipient.CurrentStep)
                    .OrderBy(x => x.StepNumber)
                    .ToListAsync(cancellationToken);

                var step = steps.FirstOrDefault(x => Matches(prospect, ParseRules(x.RulesJson)));
                if (step is null)
                {
                    recipient.Status = "completed";
                    recipient.NextRunAtUtc = null;
                    continue;
                }

                var message = new OutreachMessage
                {
                    TenantId = recipient.TenantId,
                    CampaignId = campaign.Id,
                    ProspectId = prospect.Id,
                    CampaignStepId = step.Id,
                    Channel = step.Channel,
                    Subject = RenderTemplate(step.SubjectTemplate, prospect),
                    Body = RenderTemplate(step.BodyTemplate, prospect),
                    Status = OutreachStatus.Queued
                };
                db.OutreachMessages.Add(message);

                var approvalTitle = $"APPROVAL: Send outreach {message.Id}";
                if (!await db.CrmTasks.AnyAsync(
                        x => x.TenantId == recipient.TenantId &&
                             x.Title == approvalTitle &&
                             !x.Completed,
                        cancellationToken))
                {
                    db.CrmTasks.Add(new CrmTask
                    {
                        TenantId = recipient.TenantId,
                        Title = approvalTitle,
                        DueAtUtc = DateTime.UtcNow.AddHours(4)
                    });
                }

                recipient.CurrentStep = step.StepNumber;
                recipient.Status = "awaiting-delivery";
                recipient.NextRunAtUtc = null;
                queued++;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return queued;
        });
    }

    public async Task<bool> ConfirmDeliveryAsync(Guid tenantId, Guid messageId, string providerMessageId, CancellationToken cancellationToken)
    {
        var message = await db.OutreachMessages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == messageId, cancellationToken);
        if (message is null) return false;
        message.Status = OutreachStatus.Sent;
        message.ProviderMessageId = providerMessageId.Trim();
        message.SentAtUtc = DateTime.UtcNow;

        var recipient = await db.CampaignRecipients.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.CampaignId == message.CampaignId && x.ProspectId == message.ProspectId,
            cancellationToken);
        if (recipient is not null)
        {
            var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == recipient.ProspectId, cancellationToken);
            var steps = await db.CampaignSteps
                .Where(x => x.TenantId == tenantId && x.CampaignId == message.CampaignId && x.StepNumber > recipient.CurrentStep)
                .OrderBy(x => x.StepNumber)
                .ToListAsync(cancellationToken);
            var next = prospect is null ? null : steps.FirstOrDefault(x => Matches(prospect, ParseRules(x.RulesJson)));
            recipient.Status = next is null ? "completed" : "active";
            recipient.NextRunAtUtc = next is null ? null : DateTime.UtcNow.AddHours(next.DelayHours);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private sealed record CampaignStepRules(string Qualification = "qualified", int MinimumScore = 70, string Industry = "", string Countries = "", int? CompanySizeMin = null, int? CompanySizeMax = null, string ContactRoles = "", bool StopOnReply = true);

    private static CampaignStepRules ParseRules(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<CampaignStepRules>(json) ?? new CampaignStepRules(); }
        catch { return new CampaignStepRules(); }
    }

    private static bool Matches(Prospect p, CampaignStepRules r)
    {
        if (p.Status != ProspectStatus.Qualified) return false;
        if (p.PriorityScore < Math.Clamp(r.MinimumScore, 0, 100)) return false;
        if (r.CompanySizeMin.HasValue && p.CompanySize < r.CompanySizeMin.Value) return false;
        if (r.CompanySizeMax.HasValue && p.CompanySize > r.CompanySizeMax.Value) return false;
        if (!string.IsNullOrWhiteSpace(r.Industry) && !ContainsAny(p.Industry, r.Industry)) return false;
        if (!string.IsNullOrWhiteSpace(r.Countries) && !ContainsAny(p.Country, r.Countries)) return false;
        if (!string.IsNullOrWhiteSpace(r.ContactRoles) && !ContainsAny(p.JobTitle, r.ContactRoles)) return false;
        return true;
    }

    private static bool ContainsAny(string value, string csv) => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));

    public static string RenderTemplate(string template, Prospect prospect) => template
        .Replace("{{company}}", prospect.CompanyName, StringComparison.OrdinalIgnoreCase)
        .Replace("{{contact}}", prospect.ContactName, StringComparison.OrdinalIgnoreCase)
        .Replace("{{contactName}}", prospect.ContactName, StringComparison.OrdinalIgnoreCase)
        .Replace("{{industry}}", prospect.Industry, StringComparison.OrdinalIgnoreCase)
        .Replace("{{country}}", prospect.Country, StringComparison.OrdinalIgnoreCase);
}
