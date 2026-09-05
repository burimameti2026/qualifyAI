using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public interface IAutonomousAcquisitionRunOrchestrator{Task ExecuteAsync(Guid runId,CancellationToken ct=default);}

public sealed class AutonomousAcquisitionRunOrchestrator(AppDbContext db,IAutonomousAcquisitionTemplateRegistry templates,IEnumerable<IProspectDiscoveryProvider> providers):IAutonomousAcquisitionRunOrchestrator
{
 public async Task ExecuteAsync(Guid runId,CancellationToken ct=default)
 {
  var run=await db.AutonomousAcquisitionAgentRuns.SingleOrDefaultAsync(x=>x.Id==runId,ct)??throw new InvalidOperationException("Agent run was not found.");
  if(run.Status!=AutonomousAgentRunStatus.Queued)return;
  var agent=await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(x=>x.Id==run.AgentId&&x.TenantId==run.TenantId,ct)??throw new InvalidOperationException("Agent was not found.");
  if(agent.Status is AutonomousAgentStatus.Paused or AutonomousAgentStatus.Stopped)throw new InvalidOperationException("Agent is not allowed to run.");
  run.Status=AutonomousAgentRunStatus.Running;run.StartedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync(ct);
  try
  {
   var template=templates.Apply(agent);var countries=ReadCountries(agent.CountriesJson);var country=countries.Count==0?string.Empty:countries[(int)(DateTime.UtcNow.DayOfYear%countries.Count)];
   var query=BuildQuery(template,agent,run,country);run.Query=query;
   var icp=new IcpProfile{TenantId=agent.TenantId,Name=$"Agent {agent.Name} run",Industry=string.IsNullOrWhiteSpace(agent.Industry)?template.Industry:agent.Industry,CountriesCsv=string.Join(',',countries),IntentKeywordsCsv=string.Join(',',template.Keywords),Active=true};
   var provider=providers.FirstOrDefault(x=>x.IsConfigured)??throw new InvalidOperationException("No configured prospect discovery provider is available.");
   var candidates=await provider.SearchAsync(icp,new DiscoveryRunOptions(provider.Name,string.IsNullOrWhiteSpace(agent.Region)?template.Region:agent.Region,Math.Clamp(agent.DailyDiscoveryLimit,1,100),0,CreateTargetList:false),ct);
   run.DiscoveredCount=candidates.Count;
   var existing=await db.Prospects.Where(x=>x.TenantId==agent.TenantId).Select(x=>x.Domain).ToListAsync(ct);var known=new HashSet<string>(existing.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(Normalize),StringComparer.OrdinalIgnoreCase);var now=DateTime.UtcNow;
   foreach(var c in candidates){var domain=Normalize(c.Domain);if(string.IsNullOrWhiteSpace(domain)||known.Contains(domain))continue;known.Add(domain);var score=Score(template,agent,c);var p=new Prospect{TenantId=agent.TenantId,CompanyName=c.CompanyName,Domain=domain,Industry=c.Industry??agent.Industry,Country=string.IsNullOrWhiteSpace(c.Country)?country:c.Country,Source=provider.Name,SourceUrl=c.SourceUrl,DatasetOrigin="autonomous-agent",VerificationStatus="public-source",ContactReadiness="company-only",SizeBand="unknown",SuggestedBuyer="Needs enrichment",Priority=score>=90?"high":"medium",OutreachStatus="not-ready",PainHypothesis=$"Autonomous agent score {score}/100 based on template and public evidence.",CreatedAtUtc=now,UpdatedAtUtc=now};p.Evaluate(score,score);db.Prospects.Add(p);db.ProspectSignals.Add(new ProspectSignal{TenantId=agent.TenantId,ProspectId=p.Id,Type="autonomous-discovery",Source=provider.Name,Evidence=c.Evidence,Score=score,SourceUrl=c.SourceUrl,ObservedAtUtc=now});run.QualifiedCount++;if(score>=agent.MinimumScore)run.HighScoreCount++;}
   agent.LastRunAtUtc=now;agent.UpdatedAtUtc=now;run.Status=AutonomousAgentRunStatus.Completed;run.CompletedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync(ct);
  }
  catch(Exception ex){run.Status=AutonomousAgentRunStatus.Failed;run.Error=ex.Message;run.CompletedAtUtc=DateTime.UtcNow;agent.Status=AutonomousAgentStatus.Failed;agent.UpdatedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync(CancellationToken.None);throw;}
 }
 static List<string> ReadCountries(string json){try{return JsonSerializer.Deserialize<List<string>>(json)??[];}catch{return[];}}
 static string BuildQuery(AutonomousAcquisitionTemplate t,AutonomousAcquisitionAgent a,AutonomousAcquisitionAgentRun r,string country){var keyword=t.Keywords.Count==0?a.Industry:t.Keywords[(int)(r.Id.GetHashCode()&int.MaxValue)%t.Keywords.Length];return string.Join(' ',new[]{keyword,a.Industry,country,a.Region}.Where(x=>!string.IsNullOrWhiteSpace(x)));}
 static int Score(AutonomousAcquisitionTemplate t,AutonomousAcquisitionAgent a,DiscoveryCandidate c){var text=$"{c.CompanyName} {c.Domain} {c.Evidence}".ToLowerInvariant();var hits=t.Signals.Count(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase));var keywordHits=t.Keywords.Count(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase));return Math.Clamp(45+hits*10+keywordHits*6+(string.IsNullOrWhiteSpace(c.Country)?0:8),0,100);}
 static string Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?string.Empty:value.Trim().ToLowerInvariant().Replace("https://",string.Empty).Replace("http://",string.Empty).Trim('/').Replace("www.",string.Empty);
}
