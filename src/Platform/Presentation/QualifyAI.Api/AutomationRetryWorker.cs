using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Automation;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public sealed class AutomationRetryOptions
{
    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 30;
    public int MaxAttempts { get; set; } = 3;
    public int BaseDelaySeconds { get; set; } = 30;
}

public sealed class AutomationRetryWorker(IServiceScopeFactory scopeFactory, IOptions<AutomationRetryOptions> options, ILogger<AutomationRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { if (options.Value.Enabled) await RetryDueAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Automation retry polling failed."); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 10, 3600)), stoppingToken);
        }
    }

    private async Task RetryDueAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<AutomationActionExecutor>();
        var failed = await db.AutomationRuns.Where(x => x.Status == "failed").OrderBy(x => x.CompletedAtUtc).Take(50).ToListAsync(ct);
        foreach (var previous in failed)
        {
            var attempt = ReadAttempt(previous.TriggerDataJson);
            var marker = $"\"parentRunId\":\"{previous.Id}\"";
            if (await db.AutomationRuns.AnyAsync(x => x.TriggerDataJson.Contains(marker), ct)) continue;
            if (attempt >= options.Value.MaxAttempts) { await DeadLetterAsync(db, previous, attempt, ct); continue; }
            var delay = TimeSpan.FromSeconds(options.Value.BaseDelaySeconds * Math.Pow(2, attempt));
            if ((previous.CompletedAtUtc ?? previous.UpdatedAtUtc).Add(delay) > DateTime.UtcNow) continue;
            var rule = await db.AutomationRules.FirstOrDefaultAsync(x => x.TenantId == previous.TenantId && x.Id == previous.RuleId && x.Active, ct);
            if (rule is null) { await DeadLetterAsync(db, previous, attempt, ct); continue; }
            var payload = JsonSerializer.Serialize(new { parentRunId = previous.Id, retryAttempt = attempt + 1, originalTrigger = previous.TriggerDataJson });
            var retry = AutomationRun.Create(previous.TenantId, previous.RuleId, payload);
            db.AutomationRuns.Add(retry); retry.Start(); await db.SaveChangesAsync(ct);
            var result = await executor.ExecuteAsync(rule, retry, ct);
            if (result.Success) retry.Complete(result.LogJson); else retry.Fail(result.LogJson);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Automation run {RunId} retry attempt {Attempt} finished {Status}.", previous.Id, attempt + 1, retry.Status);
        }
    }

    private static int ReadAttempt(string json)
    {
        try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty("retryAttempt", out var value) ? value.GetInt32() : 0; }
        catch { return 0; }
    }

    private static async Task DeadLetterAsync(AppDbContext db, AutomationRun run, int attempt, CancellationToken ct)
    {
        var connection = db.IntegrationConnections.Local.FirstOrDefault(x => x.TenantId == run.TenantId && x.Provider == "internal-dead-letter")
            ?? await db.IntegrationConnections.FirstOrDefaultAsync(x => x.TenantId == run.TenantId && x.Provider == "internal-dead-letter", ct);
        if (connection is null) { connection = new IntegrationConnection { TenantId = run.TenantId, Provider = "internal-dead-letter", Name = "Automation dead-letter queue", Status = IntegrationStatus.Connected }; db.IntegrationConnections.Add(connection); }
        if (!await db.IntegrationSyncJobs.AnyAsync(x => x.TenantId == run.TenantId && x.ConnectionId == connection.Id && x.EntityType == $"automation-run:{run.Id}", ct))
        {
            db.IntegrationSyncJobs.Add(new IntegrationSyncJob { TenantId = run.TenantId, ConnectionId = connection.Id, Direction = "internal", EntityType = $"automation-run:{run.Id}", Status = "dead-letter", Error = $"Automation failed after {attempt} retries. {run.LogJson}" });
            db.Notifications.Add(new Notification { TenantId = run.TenantId, Title = "Automation moved to dead-letter", Body = $"Run {run.Id} requires operator review after {attempt} retries." });
            await db.SaveChangesAsync(ct);
        }
    }
}
