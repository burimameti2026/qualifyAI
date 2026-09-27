using QualifyAI.Application;
namespace QualifyAI.Api;
public static class ApiHelpers { public static Guid TenantId(this ITenantContext tc)=>tc.Current?.Id ?? throw new InvalidOperationException("Tenant not resolved. Send JWT or X-Tenant header."); }
