using Microsoft.EntityFrameworkCore;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;
public static class BillingHardeningEndpoints
{
 public static IEndpointRouteBuilder MapBillingHardening(this IEndpointRouteBuilder e)
 {
  e.MapGet("/api/billing/tenants/{tenantId}/lifecycle",async(Guid tenantId,AppDbContext db,CancellationToken ct)=>Results.Ok(await db.TenantBillingLifecycles.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenantId,ct)));
  e.MapGet("/api/billing/tenants/{tenantId}/usage/{metric}",(Guid tenantId,string metric,IUsageMeter meter)=>Results.Ok(new{tenantId,metric,value=meter.Get(tenantId,metric)}));
  e.MapPost("/api/billing/tenants/{tenantId}/usage/{metric}",(Guid tenantId,string metric,long amount,IUsageMeter meter)=>Results.Ok(new{tenantId,metric,value=meter.Add(tenantId,metric,amount)}));
  return e;
 }
}
