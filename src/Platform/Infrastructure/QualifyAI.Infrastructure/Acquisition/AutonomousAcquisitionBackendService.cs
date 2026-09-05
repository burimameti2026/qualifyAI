using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Email;
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

public sealed class AutonomousAcquisitionBackendService(AppDbContext db,IEnumerable<IEmailDeliveryProvider> providers,IConfiguration configuration):IAutonomousAcquisitionBackendService
{
 public async Task<string> SelectNextQueryAsync(AutonomousAcquisitionAgent agent,AutonomousAcquisitionTemplate template,CancellationToken ct=default)
 {
  var countries=Read(agent.CountriesJson);if(countries.Count==0)countries.Add(agent.Region);
  var variants=(template.Keywords.Length==0?new[]{agent.Industry}:template.Keywords).SelectMany(k=>countries.Select(c=>$"{k} {agent.Industry} {c}".Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  var used=await db.AutonomousAcquisitionAgentMemories.Where(x=>x.TenantId==agent.TenantId&&x.AgentId==agent.Id&&x.Category=="query").Select(x=>x.Value).ToListAsync(ct);
  var query=variants.FirstOrDefault(v=>!used.Contains(v,StringComparer.OrdinalIgnoreCase))??variants[DateTime.UtcNow.DayOfYear%variants.Length];
  var key=$"query:{Guid.NewGuid():N}";db.AutonomousAcquisitionAgentMemories.Add(new AutonomousAcquisitionAgentMemory{TenantId=agent.TenantId,AgentId=agent.Id,Category="query",Key=key,Value=query});await Audit(agent.TenantId,"autonomous.query.selected",agent.Id,new{query},ct);await db.SaveChangesAsync(ct);return query;
 }
 public async Task<AutonomousResearchResult> ResearchAsync(Guid tenantId,Guid agentId,Prospect p,int threshold,CancellationToken ct=default)
 {
  var text=$"{p.CompanyName} {p.Domain} {p.Industry} {p.Country} {p.PainHypothesis}".ToLowerInvariant();
  var evidence=await db.ProspectSignals.Where(x=>x.TenantId==tenantId&&x.ProspectId==p.Id).Select(x=>x.Evidence).ToListAsync(ct);
  var positive=evidence.Count(x=>!string.IsNullOrWhiteSpace(x));var fit=Math.Clamp(45+positive*8+(!string.IsNullOrWhiteSpace(p.Industry)?10:0)+(!string.IsNullOrWhiteSpace(p.Country)?8:0),0,100);var intent=Math.Clamp(p.IntentScore+positive*5+(text.Contains("fleet")||text.Contains("software")||text.Contains("logistics")?8:0),0,100);p.Evaluate(fit,intent);var score=p.PriorityScore;
  var suppressed=await IsSuppressed(tenantId,p,ct);var ready=!string.IsNullOrWhiteSpace(p.Email)&&!p.Email.EndsWith(".example",StringComparison.OrdinalIgnoreCase)&&!suppressed;p.ContactReadiness=ready?"ready":"needs-contact";p.Priority=score>=threshold?"high":"medium";p.OutreachStatus=score>=threshold&&ready?"eligible":"not-ready";p.Status=score>=threshold&&!suppressed?ProspectStatus.Qualified:ProspectStatus.Enriched;
  await Audit(tenantId,"autonomous.research.scored",p.Id,new{agentId,score,threshold,suppressed,ready},ct);await db.SaveChangesAsync(ct);return new(score,string.Join(" | ",evidence.Take(5)),p.ContactReadiness,suppressed,score>=threshold&&!suppressed,score>=threshold&&!suppressed?"qualified":"nurture");
 }
 public Task<string> GenerateOutreachAsync(Prospect p,CancellationToken ct=default){var subject=$"Quick question about {p.CompanyName}";var body=$"Hi {p.ContactName},\n\nI noticed {p.CompanyName} works in {p.Industry}. Based on the public signals we found, {p.PainHypothesis}\n\nWould it be useful to compare how similar teams handle this today?\n\nBest regards";return Task.FromResult(subject+"\n\n"+body);}
 public async Task<bool> CanContactAsync(Guid tenantId,Prospect p,int dailyLimit,CancellationToken ct=default){if(await IsSuppressed(tenantId,p,ct))return false;if(string.IsNullOrWhiteSpace(p.Email)||p.Email.EndsWith(".example",StringComparison.OrdinalIgnoreCase))return false;var sent=await db.UsageRecords.CountAsync(x=>x.TenantId==tenantId&&x.Meter=="emails_sent"&&x.CreatedAtUtc>=DateTime.UtcNow.Date,ct);return sent<Math.Max(1,dailyLimit);}
 public async Task RecordReplyFeedbackAsync(Guid tenantId,Guid prospectId,string classification,int sentiment,CancellationToken ct=default){var p=await db.Prospects.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&x.Id==prospectId,ct);if(p is null)return;p.Status=classification.Equals("interested",StringComparison.OrdinalIgnoreCase)?ProspectStatus.Replied:ProspectStatus.Nurturing;p.OutreachStatus=classification;db.AutonomousAcquisitionAgentMemories.Add(new AutonomousAcquisitionAgentMemory{TenantId=tenantId,AgentId=Guid.Empty,Category="feedback",Key=$"reply:{prospectId:N}:{DateTime.UtcNow.Ticks}",Value=JsonSerializer.Serialize(new{classification,sentiment})});await Audit(tenantId,"autonomous.reply.feedback",prospectId,new{classification,sentiment},ct);await db.SaveChangesAsync(ct);}
 public async Task RetryFailedRunAsync(Guid runId,CancellationToken ct=default){var r=await db.AutonomousAcquisitionAgentRuns.SingleOrDefaultAsync(x=>x.Id==runId,ct);if(r is null||r.Status!=AutonomousAgentRunStatus.Failed)return;r.Status=AutonomousAgentRunStatus.Queued;r.Error=null;r.CompletedAtUtc=null;await Audit(r.TenantId,"autonomous.run.retry",r.Id,new{runId},ct);await db.SaveChangesAsync(ct);}
 async Task<bool> IsSuppressed(Guid tenantId,Prospect p,CancellationToken ct){if(p.Status==ProspectStatus.Suppressed)return true;if(!string.IsNullOrWhiteSpace(p.Email)){var contactId=p.ContactId??await db.Contacts.Where(x=>x.TenantId==tenantId&&x.Email==p.Email).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct);if(contactId.HasValue)return await db.ConsentRecords.AnyAsync(x=>x.TenantId==tenantId&&x.ContactId==contactId&&x.Type=="marketing"&&!x.Granted,ct);}return false;}
 Task Audit(Guid tenantId,string action,Guid entityId,object data,CancellationToken ct){db.AuditLogs.Add(new AuditLog{TenantId=tenantId,Action=action,EntityType="AutonomousAcquisition",EntityId=entityId.ToString(),DataJson=JsonSerializer.Serialize(data)});return Task.CompletedTask;}
 static List<string> Read(string json){try{return JsonSerializer.Deserialize<List<string>>(json)??[];}catch{return[];}}
}
