using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure;

public static class TenantWorkerKeys
{
    public const string AcquisitionCampaign = "acquisition-campaign";
    public const string RevenueAutomation = "revenue-automation";
    public const string AutomationScheduler = "automation-scheduler";
    public const string AutomationRetry = "automation-retry";
    public const string AutonomousAcquisitionQueue = "autonomous-acquisition-queue";
    public const string AutonomousAcquisitionScheduler = "autonomous-acquisition-scheduler";
    public const string AutonomousAcquisitionEnrichment = "autonomous-acquisition-enrichment";
    public const string OutreachDelivery = "outreach-delivery";
}

public sealed record TenantWorkerDefinition(string Key, string Name, string Description);

public sealed class TenantWorkerRuntime(AppDbContext db)
{
    public static readonly IReadOnlyList<TenantWorkerDefinition> Definitions =
    [
        new(TenantWorkerKeys.AcquisitionCampaign, "Acquisition Campaign", "Queues due campaign messages for this tenant."),
        new(TenantWorkerKeys.RevenueAutomation, "Revenue Automation", "Runs revenue and sales automation for this tenant."),
        new(TenantWorkerKeys.AutomationScheduler, "Automation Scheduler", "Runs scheduled automation rules for this tenant."),
        new(TenantWorkerKeys.AutomationRetry, "Automation Retry", "Retries failed automation runs for this tenant."),
        new(TenantWorkerKeys.AutonomousAcquisitionQueue, "Autonomous Acquisition Queue", "Executes queued autonomous acquisition runs for this tenant."),
        new(TenantWorkerKeys.AutonomousAcquisitionScheduler, "Autonomous Acquisition Scheduler", "Schedules enabled autonomous acquisition agents for this tenant."),
        new(TenantWorkerKeys.AutonomousAcquisitionEnrichment, "Autonomous Acquisition Enrichment", "Researches discovered prospects for this tenant."),
        new(TenantWorkerKeys.OutreachDelivery, "Outreach Delivery", "Sends only human-approved outreach messages for this tenant.")
    ];

    public async Task<IReadOnlySet<Guid>> EnabledTenantIdsAsync(string workerKey, CancellationToken ct = default)
        => (await db.TenantSettings.AsNoTracking()
            .Where(x => x.Key == WorkerSettingKey(workerKey) && x.Value == "true")
            .Select(x => x.TenantId)
            .ToListAsync(ct)).ToHashSet();

    public Task<List<EnabledTenant>> EnabledActiveTenantsAsync(string workerKey, CancellationToken ct = default)
    {
        var key = WorkerSettingKey(workerKey);
        var now = DateTime.UtcNow;

        return db.TenantEntitlements
            .AsNoTracking()
            .Where(x =>
                x.TenantId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(x.TenantSlug) &&
                x.TenantStatus == "active" &&
                x.LicenseStatus == "active" &&
                x.StartsAtUtc <= now &&
                (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now) &&
                db.TenantSettings.Any(s =>
                    s.TenantId == x.TenantId &&
                    s.Key == key &&
                    s.Value == "true"))
            .Select(x => new EnabledTenant(x.TenantId, x.TenantSlug!))
            .ToListAsync(ct);
    }

    public Task<List<EnabledTenant>> ActiveCampaignTenantsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return db.TenantEntitlements
            .AsNoTracking()
            .Where(x =>
                x.TenantId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(x.TenantSlug) &&
                x.TenantStatus == "active" &&
                x.LicenseStatus == "active" &&
                x.StartsAtUtc <= now &&
                (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now) &&
                db.Campaigns.Any(c =>
                    c.TenantId == x.TenantId &&
                    c.Status == CampaignStatus.Running))
            .Select(x => new EnabledTenant(x.TenantId, x.TenantSlug!))
            .ToListAsync(ct);
    }

    public static string WorkerSettingKey(string workerKey) => $"worker.enabled.{workerKey}";
}

public sealed record EnabledTenant(Guid Id, string Slug);
