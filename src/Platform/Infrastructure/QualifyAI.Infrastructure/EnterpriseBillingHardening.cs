using System.Collections.Concurrent;

namespace QualifyAI.Infrastructure;

public enum EnterpriseBillingState { Trial, Active, PastDue, GracePeriod, Suspended, Cancelled, Expired }
public sealed record BillingLifecyclePolicy(int TrialDays=14,int GraceDays=7,int MaxRetryAttempts=4,decimal RetryMultiplier=1.25m);
public sealed record BillingLifecycleSnapshot(Guid TenantId,EnterpriseBillingState State,DateTime? TrialEndsAtUtc,DateTime? GraceEndsAtUtc,int RetryAttempt,DateTime? NextRetryAtUtc,string? LastPaymentState=null);
public interface IBillingLifecycleEngine { BillingLifecycleSnapshot Transition(BillingLifecycleSnapshot current,string paymentState,DateTime nowUtc); decimal Prorate(decimal oldPrice,decimal newPrice,int periodDays,int remainingDays); }
public sealed class BillingLifecycleEngine(BillingLifecyclePolicy policy) : IBillingLifecycleEngine
{
    public BillingLifecycleSnapshot Transition(BillingLifecycleSnapshot current,string paymentState,DateTime nowUtc)
    {
        var state = paymentState.Trim().ToLowerInvariant();
        if (state is "paid" or "succeeded" or "active") return current with { State=EnterpriseBillingState.Active, GraceEndsAtUtc=null, RetryAttempt=0, NextRetryAtUtc=null, LastPaymentState=state };
        if (state is "cancelled" or "canceled") return current with { State=EnterpriseBillingState.Cancelled, LastPaymentState=state, NextRetryAtUtc=null };
        if (state is "expired") return current with { State=EnterpriseBillingState.Expired, LastPaymentState=state, NextRetryAtUtc=null };
        if (state is "past_due" or "failed" or "requires_payment_method")
        {
            var graceEnds = current.GraceEndsAtUtc ?? nowUtc.AddDays(policy.GraceDays);
            var attempt = Math.Min(current.RetryAttempt + 1, policy.MaxRetryAttempts);
            var retryHours = (int)Math.Ceiling(24 * Math.Pow((double)policy.RetryMultiplier, Math.Max(0, attempt - 1)));
            DateTime? next = attempt >= policy.MaxRetryAttempts ? null : nowUtc.AddHours(retryHours);
            var nextState = graceEnds <= nowUtc ? EnterpriseBillingState.Suspended : EnterpriseBillingState.GracePeriod;
            return current with { State=nextState, GraceEndsAtUtc=graceEnds, RetryAttempt=attempt, NextRetryAtUtc=next, LastPaymentState=state };
        }
        return current with { LastPaymentState=state };
    }
    public decimal Prorate(decimal oldPrice,decimal newPrice,int periodDays,int remainingDays)
        => periodDays <= 0 ? 0 : Math.Round((newPrice-oldPrice) * Math.Clamp(remainingDays,0,periodDays) / periodDays,2,MidpointRounding.AwayFromZero);
}

public sealed record UsageMeterKey(Guid TenantId,string Metric);
public interface IUsageMeter { long Add(Guid tenantId,string metric,long amount=1); long Get(Guid tenantId,string metric); bool IsExceeded(Guid tenantId,string metric,long limit); }
public sealed class InMemoryUsageMeter : IUsageMeter
{
    private readonly ConcurrentDictionary<UsageMeterKey,long> values = new();
    public long Add(Guid tenantId,string metric,long amount=1) => values.AddOrUpdate(new(tenantId,metric),amount,(_,v)=>v+amount);
    public long Get(Guid tenantId,string metric) => values.TryGetValue(new(tenantId,metric),out var value) ? value : 0;
    public bool IsExceeded(Guid tenantId,string metric,long limit) => limit >= 0 && Get(tenantId,metric) >= limit;
}

public sealed record BillingAlert(Guid TenantId,string Severity,string Type,string Message,DateTime OccurredAtUtc);
public interface IBillingAlertSink { Task PublishAsync(BillingAlert alert,CancellationToken ct=default); }
public sealed class NullBillingAlertSink : IBillingAlertSink { public Task PublishAsync(BillingAlert alert,CancellationToken ct=default)=>Task.CompletedTask; }

public interface IBillingProviderAdapter : IBillingProvider { bool CanHandle(string eventType); }
