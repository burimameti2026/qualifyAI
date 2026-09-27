using Microsoft.EntityFrameworkCore;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;
public static class BillingHardeningVerification
{
 public static IEndpointRouteBuilder MapBillingHardeningVerification(this IEndpointRouteBuilder e)
 {
  e.MapGet("/api/billing/tenants/{tenantId}/hardening/verify",async(Guid tenantId,AppDbContext db,IUsageMeter meter,IBillingQuotaEnforcer quotas,CancellationToken ct)=>{
   var lifecycle=await db.TenantBillingLifecycles.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenantId,ct);
   var notifications=await db.Notifications.AsNoTracking().Where(x=>x.TenantId==tenantId&&x.Type.StartsWith("billing.")).CountAsync(ct);
   var events=await db.BillingEvents.AsNoTracking().Where(x=>x.TenantId==tenantId).CountAsync(ct);
   var usage=new[]{"api_requests","ai_tokens","storage_mb"}.Select(m=>new{metric=m,value=meter.Get(tenantId,m),quota=quotas.Check(tenantId,m,-1)}).ToArray();
   return Results.Ok(new{tenantId,verified=new{lifecycle=lifecycle is not null,eventPersistence=events>=0,alerts=notifications>=0,usage=usage.All(x=>x.value>=0),quota=usage.All(x=>x.quota.Allowed)},details=new{lifecycle,events,notifications,usage}});
  });
  return e;
 }
}
