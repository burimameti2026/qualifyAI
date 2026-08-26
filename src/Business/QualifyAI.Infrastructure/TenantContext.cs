using QualifyAI.Application;
namespace QualifyAI.Infrastructure;
public sealed class TenantContext:ITenantContext { public CurrentTenant? Current { get; private set; } public void Set(CurrentTenant tenant)=>Current=tenant; }
