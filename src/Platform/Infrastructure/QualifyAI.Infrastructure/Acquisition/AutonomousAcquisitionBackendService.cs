using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed record AutonomousResearchResult(int Score,string Evidence,string ContactReadiness,bool Suppressed,bool Qualified,string Decision);

public interface IAutonomousAcquisitionBackendService
{
 Task<string> SelectNextQueryAsync(AutonomousAcquisitionAgent agent,AutonomousAcquisitionTemplate template,CancellationToken ct=default);
 Task<AutonomousResearchResult> ResearchAsync(Guid tenantId,Guid agentId,Prospect prospect,int threshold,CancellationToken ct=default);
 Task<string> GenerateOutreachAsync(Prospect prospect,CancellationToken ct=default);
 Task<bool> CanContactAsync(Guid tenantId,Prospect prospect,int dailyLimit,CancellationToken ct=default);
 Task RecordReplyFeedbackAsync(Guid tenantId,Guid prospectId,string classification,int sentiment,CancellationToken ct=default);
 Task RetryFailedRunAsync(Guid runId,CancellationToken ct=default);
}

public sealed class AutonomousAcquisitionBackendService(AppDbContext db):IAutonomousAcquisitionBackendService
{
 public async Task<string> SelectNextQueryAsync(AutonomousAcquisitionAgent agent,AutonomousAcquisitionTemplate template,CancellationToken ct=default)
 {
  var countries=Read(agent.CountriesJson);if(countries.Count==0)countries.Add(agent.Region);
  var variants=(template.Keywords.Length==0?new[]{agent.Industry}:template.Keywords)
   .SelectMany(k=>countries.Select(c=>$"{k} {c}".Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if(variants.Length==0)variants=new[]{agent.Industry};
  var used=await db.AutonomousAcquisitionAgentMemories.Where(x=>x.TenantId==agent.TenantId&&x.AgentId==agent.Id&&x.Category=="query").Select(x=>x.Value).ToListAsync(ct);
  var query=variants.FirstOrDefault(v=>!used.Contains(v,StringComparer.OrdinalIgnoreCase))??variants[DateTime.UtcNow.DayOfYear%variants.Length];
  db.AutonomousAcquisitionAgentMemories.Add(new AutonomousAcquisitionAgentMemory{TenantId=agent.TenantId,AgentId=agent.Id,Category="query",Key=$"query:{Guid.NewGuid():N}",Value=query});
  await Audit(agent.TenantId,"autonomous.query.selected",agent.Id,new{query},ct);await db.SaveChangesAsync(ct);return query;
 }

 public async Task<AutonomousResearchResult> ResearchAsync(Guid tenantId,Guid agentId,Prospect p,int threshold,CancellationToken ct=default)
 {
  var text=$"{p.CompanyName} {p.Domain} {p.Industry} {p.Country} {p.PainHypothesis}".ToLowerInvariant();
  var evidence=await db.ProspectSignals.Where(x=>x.TenantId==tenantId&&x.ProspectId==p.Id).Select(x=>x.Evidence).ToListAsync(ct);
  var positive=evidence.Count(x=>!string.IsNullOrWhiteSpace(x));
  var fit=Math.Clamp(45+positive*8+(!string.IsNullOrWhiteSpace(p.Industry)?10:0)+(!string.IsNullOrWhiteSpace(p.Country)?8:0),0,100);
  var intent=Math.Clamp(p.IntentScore+positive*5+(text.Contains("fleet")||text.Contains("software")||text.Contains("logistics")?8:0),0,100);
  p.Evaluate(fit,intent);
  var suppressed=await IsSuppressed(tenantId,p,ct);
  var ready=!string.IsNullOrWhiteSpace(p.Email)&&!p.Email.EndsWith(".example",StringComparison.OrdinalIgnoreCase)&&!suppressed;
  p.ContactReadiness=ready?"ready":"needs-contact";p.Priority=p.PriorityScore>=threshold?"high":"medium";
  p.OutreachStatus=p.PriorityScore>=threshold&&ready?"eligible":"not-ready";
  p.Status=p.PriorityScore>=threshold&&!suppressed?ProspectStatus.Qualified:ProspectStatus.Enriched;
  await Audit(tenantId,"autonomous.research.scored",p.Id,new{agentId,score=p.PriorityScore,threshold,suppressed,ready},ct);
  await db.SaveChangesAsync(ct);
  return new(p.PriorityScore,string.Join(" | ",evidence.Take(5)),p.ContactReadiness,suppressed,p.PriorityScore>=threshold&&!suppressed,p.PriorityScore>=threshold&&!suppressed?"qualified":"nurture");
 }

 public async Task<string> GenerateOutreachAsync(Prospect p,CancellationToken ct=default)
 {
  var logistics=p.Industry.Contains("logistics",StringComparison.OrdinalIgnoreCase)||
                p.Industry.Contains("transport",StringComparison.OrdinalIgnoreCase)||
                p.Industry.Contains("distribution",StringComparison.OrdinalIgnoreCase)||
                p.PainHypothesis.Contains("logistics",StringComparison.OrdinalIgnoreCase);
  var subject=logistics?"A practical way to automate logistics operations":$"A practical way to automate operations at {p.CompanyName}";
  var body=logistics
   ? $"Hi {SafeName(p.ContactName)},\n\nI noticed {p.CompanyName} is operating in {p.Industry}. Logistics teams often lose time to repetitive coordination, email follow-ups and manual exception handling.\n\nWe provide logistics software that automates these processes and gives operations teams a single workflow for customer, shipment and exception management.\n\nWould a short conversation be useful to see whether this fits {p.CompanyName}?\n\nBest regards"
   : $"Hi {SafeName(p.ContactName)},\n\nI noticed {p.CompanyName} is operating in {p.Industry}. We help teams automate repetitive operational workflows and reduce manual coordination.\n\nWould a short conversation be useful to compare the current process with what can be automated?\n\nBest regards";
  return await Task.FromResult(subject+"\n\n"+body);
 }

 public async Task<bool> CanContactAsync(Guid tenantId,Prospect p,int dailyLimit,CancellationToken ct=default)
 {
  if(await IsSuppressed(tenantId,p,ct))return false;
  if(string.IsNullOrWhiteSpace(p.Email)||p.Email.EndsWith(".example",StringComparison.OrdinalIgnoreCase))return false;
  var sent=await db.UsageRecords.CountAsync(x=>x.TenantId==tenantId&&x.Meter=="emails_sent"&&x.CreatedAtUtc>=DateTime.UtcNow.Date,ct);
  return sent<Math.Max(1,dailyLimit);
 }

 public async Task RecordReplyFeedbackAsync(Guid tenantId,Guid prospectId,string classification,int sentiment,CancellationToken ct=default)
 {
  var p=await db.Prospects.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&x.Id==prospectId,ct);if(p is null)return;
  p.Status=classification.Equals("interested",StringComparison.OrdinalIgnoreCase)?ProspectStatus.Replied:ProspectStatus.Nurturing;p.OutreachStatus=classification;
  db.AutonomousAcquisitionAgentMemories.Add(new AutonomousAcquisitionAgentMemory{TenantId=tenantId,AgentId=Guid.Empty,Category="feedback",Key=$"reply:{prospectId:N}:{DateTime.UtcNow.Ticks}",Value=JsonSerializer.Serialize(new{classification,sentiment})});
  await Audit(tenantId,"autonomous.reply.feedback",prospectId,new{classification,sentiment},ct);await db.SaveChangesAsync(ct);
 }

 public async Task RetryFailedRunAsync(Guid runId,CancellationToken ct=default)
 {
  var r=await db.AutonomousAcquisitionAgentRuns.SingleOrDefaultAsync(x=>x.Id==runId,ct);if(r is null||r.Status!=AutonomousAgentRunStatus.Failed)return;
  r.Status=AutonomousAgentRunStatus.Queued;r.Error=null;r.CompletedAtUtc=null;await Audit(r.TenantId,"autonomous.run.retry",r.Id,new{runId},ct);await db.SaveChangesAsync(ct);
 }

 async Task<bool> IsSuppressed(Guid tenantId,Prospect p,CancellationToken ct)
 {
  if(p.Status==ProspectStatus.Suppressed)return true;
  if(!string.IsNullOrWhiteSpace(p.Email))
  {
   var contactId=p.ContactId??await db.Contacts.Where(x=>x.TenantId==tenantId&&x.Email==p.Email).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct);
   if(contactId.HasValue)return await db.ConsentRecords.AnyAsync(x=>x.TenantId==tenantId&&x.ContactId==contactId&&x.Type=="marketing"&&!x.Granted,ct);
  }
  return false;
 }

 Task Audit(Guid tenantId,string action,Guid entityId,object data,CancellationToken ct)
 {
  db.AuditLogs.Add(new AuditLog{TenantId=tenantId,Action=action,EntityType="AutonomousAcquisition",EntityId=entityId.ToString(),DataJson=JsonSerializer.Serialize(data)});
  return Task.CompletedTask;
 }

 static List<string> Read(string json){try{return JsonSerializer.Deserialize<List<string>>(json)??[];}catch{return[];}}
 static string SafeName(string? value)=>string.IsNullOrWhiteSpace(value)?"there":value.Trim();
}
