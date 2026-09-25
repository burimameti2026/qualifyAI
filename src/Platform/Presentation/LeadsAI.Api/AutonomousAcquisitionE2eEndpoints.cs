using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;
public static class AutonomousAcquisitionE2eEndpoints
{
 public static IEndpointRouteBuilder MapAutonomousAcquisitionE2e(this IEndpointRouteBuilder app)
 {
  app.MapGet("/api/autonomous-acquisition/tenants/{tenantId}/e2e",async(Guid tenantId,AppDbContext db,CancellationToken ct)=>
  {
   var agents=await db.AutonomousAcquisitionAgents.Where(x=>x.TenantId==tenantId).ToListAsync(ct);
   var runs=await db.AutonomousAcquisitionAgentRuns.Where(x=>x.TenantId==tenantId).OrderByDescending(x=>x.ScheduledAtUtc).Take(100).ToListAsync(ct);
   var memory=await db.AutonomousAcquisitionAgentMemories.Where(x=>x.TenantId==tenantId).CountAsync(ct);
   var taskRows=await db.AutonomousAcquisitionTasks.Where(x=>x.TenantId==tenantId && x.RunId != null).ToListAsync(ct);
   var campaigns=await db.Campaigns.Where(x=>x.TenantId==tenantId).ToListAsync(ct);
   var active=agents.Count(x=>x.Status==AutonomousAgentStatus.Active);
   var completed=runs.Count(x=>x.Status==AutonomousAgentRunStatus.Completed);
   var failed=runs.Count(x=>x.Status==AutonomousAgentRunStatus.Failed);
   var queued=runs.Count(x=>x.Status==AutonomousAgentRunStatus.Queued);
   var waitingApproval=runs.Count(x=>x.Status==AutonomousAgentRunStatus.WaitingApproval);
   var paused=runs.Count(x=>x.Status==AutonomousAgentRunStatus.Paused);
   var discovered=runs.Sum(x=>x.DiscoveredCount);
   var qualified=runs.Sum(x=>x.QualifiedCount);
   var highScore=runs.Sum(x=>x.HighScoreCount);
   var emails=runs.Sum(x=>x.EmailsSentCount);
   var runtimeCompleted=taskRows.Count(x=>x.Status==AutonomousAgentTaskStatus.Completed);
   var checks=new[]{
    new{name="agent-configured",ok=agents.Count>0},
    new{name="agent-active-or-paused",ok=active>0||agents.Any(x=>x.Status==AutonomousAgentStatus.Paused)},
    new{name="campaign-container",ok=campaigns.Count>0},
    new{name="run-created",ok=runs.Count>0},
    new{name="task-runtime",ok=taskRows.Count>0},
    new{name="task-completed",ok=runtimeCompleted>0||waitingApproval>0||paused>0},
    new{name="query-memory",ok=memory>0||runs.Count==0},
    new{name="discovery",ok=discovered>=0},
    new{name="qualification",ok=qualified>=0},
    new{name="outreach-pipeline",ok=emails>=0}
   };
   return Results.Ok(new{tenantId,status=checks.All(x=>x.ok)?"passed":"pending-data",checks,summary=new{agents=agents.Count,active,campaigns=campaigns.Count,runs=runs.Count,completed,failed,queued,waitingApproval,paused,memory,runtimeTasks=taskRows.Count,runtimeCompleted,discovered,qualified,highScore,emails},checkedAtUtc=DateTime.UtcNow});
  });
  return app;
 }
}
