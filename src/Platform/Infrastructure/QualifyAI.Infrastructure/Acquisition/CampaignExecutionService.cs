using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed class CampaignExecutionService(AppDbContext db)
{
    public async Task<int> QueueDueMessagesAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
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
            var campaign = await db.Campaigns.FirstOrDefaultAsync(
                x => x.Id == recipient.CampaignId && x.TenantId == recipient.TenantId && x.Status == CampaignStatus.Running,
                cancellationToken);
            if (campaign is null) continue;

            var prospect = await db.Prospects.FirstOrDefaultAsync(
                x => x.Id == recipient.ProspectId && x.TenantId == recipient.TenantId,
                cancellationToken);
            if (prospect is null || prospect.Status == ProspectStatus.Suppressed)
            {
                recipient.Status = "suppressed";
                continue;
            }

            var step = await db.CampaignSteps
                .Where(x => x.TenantId == recipient.TenantId && x.CampaignId == campaign.Id && x.StepNumber > recipient.CurrentStep)
                .OrderBy(x => x.StepNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (step is null)
            {
                recipient.Status = "completed";
                continue;
            }

            db.OutreachMessages.Add(new OutreachMessage
            {
                TenantId = recipient.TenantId,
                CampaignId = campaign.Id,
                ProspectId = prospect.Id,
                CampaignStepId = step.Id,
                Channel = step.Channel,
                Subject = Render(step.SubjectTemplate, prospect),
                Body = Render(step.BodyTemplate, prospect),
                Status = OutreachStatus.Queued
            });
            recipient.CurrentStep = step.StepNumber;
            recipient.Status = "awaiting-delivery";
            recipient.NextRunAtUtc = null;
            queued++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return queued;
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
            var next = await db.CampaignSteps
                .Where(x => x.TenantId == tenantId && x.CampaignId == message.CampaignId && x.StepNumber > recipient.CurrentStep)
                .OrderBy(x => x.StepNumber)
                .FirstOrDefaultAsync(cancellationToken);
            recipient.Status = next is null ? "completed" : "active";
            recipient.NextRunAtUtc = next is null ? null : DateTime.UtcNow.AddHours(next.DelayHours);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Render(string template, Prospect prospect) => template
        .Replace("{{company}}", prospect.CompanyName, StringComparison.OrdinalIgnoreCase)
        .Replace("{{contact}}", prospect.ContactName, StringComparison.OrdinalIgnoreCase)
        .Replace("{{industry}}", prospect.Industry, StringComparison.OrdinalIgnoreCase)
        .Replace("{{country}}", prospect.Country, StringComparison.OrdinalIgnoreCase);
}
