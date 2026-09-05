namespace QualifyAI.Infrastructure;

public sealed record QuotaCheck(Guid TenantId,string Metric,long Used,long Limit,bool Allowed);
public interface IBillingQuotaEnforcer { QuotaCheck Check(Guid tenantId,string metric,long limit); QuotaCheck Consume(Guid tenantId,string metric,long limit,long amount=1); }
public sealed class BillingQuotaEnforcer(IUsageMeter meter) : IBillingQuotaEnforcer
{
 public QuotaCheck Check(Guid tenantId,string metric,long limit){var used=meter.Get(tenantId,metric);return new(tenantId,metric,used,limit,limit<0||used<limit);}
 public QuotaCheck Consume(Guid tenantId,string metric,long limit,long amount=1){var before=Check(tenantId,metric,limit);if(!before.Allowed)return before;var used=meter.Add(tenantId,metric,amount);return new(tenantId,metric,used,limit,limit<0||used<=limit);}
}
