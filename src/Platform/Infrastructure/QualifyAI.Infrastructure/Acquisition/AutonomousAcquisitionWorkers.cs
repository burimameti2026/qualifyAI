using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed class AutonomousAcquisitionQueuedRunWorker(IServiceScopeFactory scopes,ILogger<AutonomousAcquisitionQueuedRunWorker> log):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  while(!stoppingToken.IsCancellationRequested){try{using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var ids=await db.AutonomousAcquisitionAgentRuns.Where(x=>x.Status==AutonomousAgentRunStatus.Queued).OrderBy(x=>x.ScheduledAtUtc).Take(10).Select(x=>x.Id).ToListAsync(stoppingToken);var orchestrator=scope.ServiceProvider.GetRequiredService<IAutonomousAcquisitionRunOrchestrator>();foreach(var id in ids){if(stoppingToken.IsCancellationRequested)break;try{await orchestrator.ExecuteAsync(id,stoppingToken);}catch(Exception ex){log.LogError(ex,"Autonomous acquisition run {RunId} failed",id);}}}catch(Exception ex){log.LogError(ex,"Autonomous acquisition queue worker iteration failed");}await Task.Delay(TimeSpan.FromSeconds(15),stoppingToken);}
 }
}

public sealed class AutonomousAcquisitionSchedulerWorker(IServiceScopeFactory scopes,ILogger<AutonomousAcquisitionSchedulerWorker> log):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  while(!stoppingToken.IsCancellationRequested){try{using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var now=DateTime.UtcNow;var today=DateOnly.FromDateTime(now);var agents=await db.AutonomousAcquisitionAgents.Where(x=>x.Status==AutonomousAgentStatus.Active).ToListAsync(stoppingToken);foreach(var a in agents){var due=now.TimeOfDay>=a.RunTimeUtc.ToTimeSpan();var already=a.LastRunAtUtc.HasValue&&DateOnly.FromDateTime(a.LastRunAtUtc.Value)==today;if(!due||already)continue;db.AutonomousAcquisitionAgentRuns.Add(new AutonomousAcquisitionAgentRun{TenantId=a.TenantId,AgentId=a.Id,IsManual=false,Status=AutonomousAgentRunStatus.Queued,ScheduledAtUtc=now});a.LastRunAtUtc=now;a.UpdatedAtUtc=now;}await db.SaveChangesAsync(stoppingToken);}catch(Exception ex){log.LogError(ex,"Autonomous acquisition scheduler iteration failed");}await Task.Delay(TimeSpan.FromMinutes(1),stoppingToken);}
 }
}
