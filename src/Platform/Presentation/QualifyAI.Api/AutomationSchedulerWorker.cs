using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Automation;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public sealed class AutomationSchedulerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
    public int WeekdayHourUtc { get; set; } = 8;
    public bool RunOnStartup { get; set; } = true;
}

public sealed class AutomationSchedulerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationSchedulerOptions> options,
    ILogger<AutomationSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) { logger.LogInformation("Automation scheduler is disabled."); return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunDueAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Scheduled automation polling failed."); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervalSeconds, 10, 3600)), stoppingToken);
        }
    }

    private async Task RunDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var weekday = now.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        if ((!weekday || now.Hour < options.Value.WeekdayHourUtc) && !options.Value.RunOnStartup) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<AutomationActionExecutor>();
        var rules = await db.AutomationRules.Where(x => x.Active && x.Trigger == "schedule.weekday")
            .OrderBy(x => x.TenantId).ThenBy(x => x.Id).ToListAsync(ct);

        foreach (var rule in rules)
        {
            var triggerData = JsonSerializer.Serialize(new { scheduleKey = $"{now:yyyy-MM-dd}:weekday", trigger = rule.Trigger });
            if (await db.AutomationRuns.AnyAsync(x => x.TenantId == rule.TenantId && x.RuleId == rule.Id && x.TriggerDataJson == triggerData, ct)) continue;
            var run = AutomationRun.Create(rule.TenantId, rule.Id, triggerData);
            db.AutomationRuns.Add(run); run.Start(); await db.SaveChangesAsync(ct);
            var result = await executor.ExecuteAsync(rule, run, ct);
            if (result.Success) run.Complete(result.LogJson); else run.Fail(result.LogJson);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Scheduled automation {RuleId} finished {Status} for tenant {TenantId}.", rule.Id, run.Status, rule.TenantId);
        }
    }
}
