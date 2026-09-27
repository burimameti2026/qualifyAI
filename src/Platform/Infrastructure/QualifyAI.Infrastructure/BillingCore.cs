using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Persistence.SqlServer.Projections;

namespace QualifyAI.Infrastructure;

public sealed record BillingProviderEvent(string Provider,string EventId,string Type,Guid TenantId,string Status,DateTime OccurredAtUtc,IReadOnlyDictionary<string,string>? Data=null);
public sealed record BillingSubscription(Guid TenantId,string Provider,string ExternalSubscriptionId,string Plan,string Status,DateTime StartedAtUtc,DateTime? EndsAtUtc=null);
public interface IBillingProvider { string Name { get; } Task HandleAsync(BillingProviderEvent item,CancellationToken ct=default); }
public interface IBillingEventProcessor { Task<bool> ProcessAsync(BillingProviderEvent item,CancellationToken ct=default); }

public sealed class BillingEventProcessor(AppDbContext db, ITenantLifecycleEventStore events, ILicenseChangeOrchestrator licenses, IBillingLifecycleEngine lifecycle, IBillingAlertSink alerts) : IBillingEventProcessor
{
 public async Task<bool> ProcessAsync(BillingProviderEvent item,CancellationToken ct=default)
 {
  var provider=item.Provider.Trim().ToLowerInvariant(); if(await db.BillingEvents.AnyAsync(x=>x.Provider==provider&&x.ExternalEventId==item.EventId,ct)) return false;
  db.BillingEvents.Add(new BillingEventRecord{Provider=provider,ExternalEventId=item.EventId,Type=item.Type,TenantId=item.TenantId,Status=item.Status,DataJson=item.Data is null?null:JsonSerializer.Serialize(item.Data),OccurredAtUtc=item.OccurredAtUtc}); await db.SaveChangesAsync(ct);
  var currentRecord=await db.TenantBillingLifecycles.SingleOrDefaultAsync(x=>x.TenantId==item.TenantId,ct);
  var current=currentRecord is null ? new BillingLifecycleSnapshot(item.TenantId,EnterpriseBillingState.Active,null,null,0,null) : new BillingLifecycleSnapshot(item.TenantId,Enum.TryParse<EnterpriseBillingState>(currentRecord.State,true,out var s)?s:EnterpriseBillingState.Active,currentRecord.TrialEndsAtUtc,currentRecord.GraceEndsAtUtc,currentRecord.RetryAttempt,currentRecord.NextRetryAtUtc,currentRecord.LastPaymentState);
  var next=lifecycle.Transition(current,item.Status,item.OccurredAtUtc);
  if(currentRecord is null){currentRecord=new TenantBillingLifecycleRecord{TenantId=item.TenantId};db.TenantBillingLifecycles.Add(currentRecord);} currentRecord.State=next.State.ToString();currentRecord.TrialEndsAtUtc=next.TrialEndsAtUtc;currentRecord.GraceEndsAtUtc=next.GraceEndsAtUtc;currentRecord.RetryAttempt=next.RetryAttempt;currentRecord.NextRetryAtUtc=next.NextRetryAtUtc;currentRecord.LastPaymentState=next.LastPaymentState;currentRecord.UpdatedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync(ct);
  events.Record(new(item.TenantId,"billing",next.State.ToString(),$"Billing event {item.Type}",item.OccurredAtUtc,item.Data,$"{provider}:{item.EventId}",provider)); if(next.State is EnterpriseBillingState.GracePeriod or EnterpriseBillingState.Suspended) await alerts.PublishAsync(new(item.TenantId,next.State.ToString(),"payment",$"Billing is {next.State}.",item.OccurredAtUtc),ct); if(next.State is EnterpriseBillingState.Suspended or EnterpriseBillingState.Cancelled or EnterpriseBillingState.Expired) await licenses.ReconcileAsync(item.TenantId,ct); return true;
 }
}

public sealed class BillingProviderRegistry(IEnumerable<IBillingProvider> providers){public IBillingProvider Get(string provider)=>providers.First(x=>string.Equals(x.Name,provider,StringComparison.OrdinalIgnoreCase));}
