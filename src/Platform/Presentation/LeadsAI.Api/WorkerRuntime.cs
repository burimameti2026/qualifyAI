using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class TenantWorkerKeys
{
    public const string AcquisitionCampaign = "acquisition-campaign";
    public const string RevenueAutomation = "revenue-automation";
    public const string AutomationScheduler = "automation-scheduler";
    public const string AutomationRetry = "automation-retry";
}

public sealed record TenantWorkerDefinition(string Key, string Name, string Description);

public sealed class TenantWorkerRuntime(AppDbContext db)
{
    public static readonly IReadOnlyList<TenantWorkerDefinition> Definitions =
    [
        new(TenantWorkerKeys.AcquisitionCampaign, "Acquisition Campaign", "Queues due campaign messages for this tenant."),
        new(TenantWorkerKeys.RevenueAutomation, "Revenue Automation", "Runs revenue and sales automation for this tenant."),
        new(TenantWorkerKeys.AutomationScheduler, "Automation Scheduler", "Runs scheduled automation rules for this tenant."),
        new(TenantWorkerKeys.AutomationRetry, "Automation Retry", "Retries failed automation runs for this tenant.")
    ];

    public async Task<IReadOnlySet<Guid>> EnabledTenantIdsAsync(string workerKey, CancellationToken ct = default)
        => (await db.TenantSettings.AsNoTracking()
            .Where(x => x.Key == WorkerSettingKey(workerKey) && x.Value == "true")
            .Select(x => x.TenantId)
            .ToListAsync(ct)).ToHashSet();

    public static string WorkerSettingKey(string workerKey) => $"worker.enabled.{workerKey}";
}
