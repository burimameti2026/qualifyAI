using LeadsAI.Application;
namespace LeadsAI.Infrastructure;
public sealed class TenantContext:ITenantContext { public CurrentTenant? Current { get; private set; } public void Set(CurrentTenant tenant)=>Current=tenant; }
