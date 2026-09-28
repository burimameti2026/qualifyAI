using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadsAI.Application;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Domain.Core;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed class AutonomousAcquisitionSchedulerWorker(IServiceScopeFactory scopes, ILogger<AutonomousAcquisitionSchedulerWorker> log) : BackgroundService
{
    private const string TimeZoneSetting = "acquisition.schedule.timeZoneId";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var rootScope = scopes.CreateAsyncScope();
                var services = rootScope.ServiceProvider;
                var runtime = services.GetRequiredService<TenantWorkerRuntime>();
                var tenants = await runtime.ActiveCampaignTenantsAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ScheduleTenantDatabaseAsync(services, tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition scheduler iteration failed"); }
        }
    }

    private async Task ScheduleTenantDatabaseAsync(IServiceProvider rootServices, Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));
        await ScheduleScopeAsync(scope.ServiceProvider, ct);
    }

    private static async Task ScheduleScopeAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var tenantId = services.GetRequiredService<ITenantContext>().Current?.Id
            ?? throw new InvalidOperationException("Tenant context is not set.");

        var agents = await db.AutonomousAcquisitionAgents
            .Where(x => x.TenantId == tenantId &&
                        x.Status == AutonomousAgentStatus.Active &&
                        db.Campaigns.Any(c => c.TenantId == tenantId &&
                                               c.AgentId == x.Id &&
                                               c.Status == CampaignStatus.Running))
            .ToListAsync(ct);

        if (agents.Count == 0) return;

        var timeZoneId = await db.TenantSettings
            .Where(x => x.TenantId == tenantId && x.Key == TimeZoneSetting)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        var timeZone = ResolveTimeZone(timeZoneId);
        var pendingAgentIds = await db.AgentJobs
            .Where(x => x.TenantId == tenantId &&
                        (x.Status == AgentJobStatus.Queued ||
                         x.Status == AgentJobStatus.Running ||
                         x.Status == AgentJobStatus.Waiting))
            .Select(x => x.AgentId)
            .Distinct()
            .ToListAsync(ct);

        var pending = pendingAgentIds.ToHashSet();
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localToday = DateOnly.FromDateTime(localNow);
        var changed = false;
        var jobFactory = services.GetRequiredService<IAgentJobFactory>();
        var containers = services.GetRequiredService<ICampaignContainerRuntime>();

        foreach (var agent in agents)
        {
            if (pending.Contains(agent.Id)) continue;
            var campaign = await db.Campaigns
                .Where(x => x.TenantId == tenantId && x.AgentId == agent.Id && x.Status == CampaignStatus.Running)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (campaign is null) continue;

            var due = localNow.TimeOfDay >= agent.RunTimeUtc.ToTimeSpan();
            var already = agent.LastRunAtUtc.HasValue &&
                          DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agent.LastRunAtUtc.Value, timeZone)) == localToday;

            if (!due || already) continue;

            var container = await containers.EnsureAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                $"{campaign.Name} Container",
                campaign.PackageCode,
                campaign.PackageVersion,
                campaign.PlanJson,
                ct);

            await jobFactory.QueueCampaignAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                container.Id,
                "campaign.execute",
                $"campaign:{campaign.Id}",
                false,
                ct);

            pending.Add(agent.Id);
            agent.LastRunAtUtc = nowUtc;
            agent.UpdatedAtUtc = nowUtc;
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }
}
