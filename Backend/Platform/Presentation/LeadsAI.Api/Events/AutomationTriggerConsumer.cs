using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Automation.Application.IntegrationEvents;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Automation;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public sealed class AutomationTriggerConsumer(AppDbContext db, AutomationActionExecutor executor) : IConsumer<AutomationTriggeredIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AutomationTriggeredIntegrationEvent> context)
    {
        var message = context.Message;
        var rule = await db.AutomationRules.FirstOrDefaultAsync(x => x.TenantId == message.TenantId && x.Id == message.AggregateId && x.Active, context.CancellationToken);
        if (rule is null) return;
        var triggerData = JsonSerializer.Serialize(new { eventId = message.EventId, correlationId = message.CorrelationId, occurredAtUtc = message.OccurredAtUtc, trigger = rule.Trigger });
        if (await db.AutomationRuns.AnyAsync(x => x.TenantId == message.TenantId && x.RuleId == rule.Id && x.TriggerDataJson == triggerData, context.CancellationToken)) return;
        var run = AutomationRun.Create(message.TenantId, rule.Id, triggerData);
        db.AutomationRuns.Add(run); run.Start(); await db.SaveChangesAsync(context.CancellationToken);
        var result = await executor.ExecuteAsync(rule, run, context.CancellationToken);
        if (result.Success) run.Complete(result.LogJson); else run.Fail(result.LogJson);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
