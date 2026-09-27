using Microsoft.EntityFrameworkCore;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;
public static class AutonomousAcquisitionVerificationEndpoints
{
 public static IEndpointRouteBuilder MapAutonomousAcquisitionVerification(this IEndpointRouteBuilder app)
 {
  app.MapGet("/api/autonomous-acquisition/verification",async(AppDbContext db,IAutonomousAcquisitionRunOrchestrator orchestrator,IAutonomousAcquisitionTemplateRegistry templates,CancellationToken ct)=>
  {
   var canConnect=await db.Database.CanConnectAsync(ct);
   var agents=await db.AutonomousAcquisitionAgents.CountAsync(ct);
   var runs=await db.AutonomousAcquisitionAgentRuns.CountAsync(ct);
   var memory=await db.AutonomousAcquisitionAgentMemories.CountAsync(ct);
   var templateCount=templates.List().Count();
   return Results.Ok(new{status=canConnect&&templateCount>0?"ready":"degraded",database=canConnect,orchestrator=orchestrator is not null,templates=templateCount,agents,runs,memory,checkedAtUtc=DateTime.UtcNow});
  });
  return app;
 }
}
