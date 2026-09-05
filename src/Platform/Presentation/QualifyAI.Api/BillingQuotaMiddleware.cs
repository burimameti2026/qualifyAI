using QualifyAI.Application;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

public sealed class BillingQuotaMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> MeteredPrefixes=new(StringComparer.OrdinalIgnoreCase){"/api/ai","/api/knowledge","/api/acquisition","/api/automation","/api/campaigns"};
    public async Task InvokeAsync(HttpContext context,ITenantContext tenantContext,IBillingQuotaEnforcer quotas,IConfiguration configuration)
    {
        var current=tenantContext.Current;
        var path=context.Request.Path.Value??string.Empty;
        if(current is null||!MeteredPrefixes.Any(path.StartsWith)||context.Request.Method is "OPTIONS") {await next(context);return;}
        var metric=path.StartsWith("/api/ai")?"ai_tokens":path.StartsWith("/api/knowledge")?"storage_mb":"api_requests";
        var key=$"Billing:Quotas:{metric}";var limit=configuration.GetValue<long?>(key)??-1;
        var check=quotas.Consume(current.TenantId,metric,limit,1);
        if(!check.Allowed){context.Response.StatusCode=StatusCodes.Status429TooManyRequests;context.Response.Headers.RetryAfter="3600";await context.Response.WriteAsJsonAsync(new{error="quota_exceeded",check.metric,check.used,check.limit});return;}
        await next(context);
    }
}
