using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Persistence.SqlServer.Projections;

namespace LeadsAI.Infrastructure;

public sealed record ModuleDefinition(string Code, IReadOnlyCollection<string> Dependencies);
public interface IModuleProvisioner { string ModuleCode { get; } Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default); }
public interface IModuleLifecycleHandler { string ModuleCode { get; } Task DeactivateAsync(Guid tenantId, CancellationToken cancellationToken = default); }
public interface IModuleRegistry { IReadOnlyCollection<ModuleDefinition> Modules { get; } IReadOnlyCollection<string> Resolve(IReadOnlyCollection<string> requestedModules); }

public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, ModuleDefinition> _modules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crm"] = new("crm", Array.Empty<string>()),
        ["golden_pipeline"] = new("golden_pipeline", new[] { "crm" }),
        ["production"] = new("production", new[] { "crm" }),
        ["bom"] = new("bom", new[] { "production" }),
        ["quality"] = new("quality", new[] { "production" }),
        ["maintenance"] = new("maintenance", new[] { "production" }),
        ["suppliers"] = new("suppliers", new[] { "crm" }),
        ["shipments"] = new("shipments", new[] { "crm" }),
        ["routes"] = new("routes", new[] { "crm" }),
        ["fleet"] = new("fleet", new[] { "crm" }),
        ["drivers"] = new("drivers", new[] { "fleet" }),
        ["dispatch"] = new("dispatch", new[] { "routes", "drivers" }),
        ["inventory"] = new("inventory", Array.Empty<string>()),
        ["receiving"] = new("receiving", new[] { "inventory" }),
        ["putaway"] = new("putaway", new[] { "receiving", "inventory" }),
        ["picking"] = new("picking", new[] { "inventory" }),
        ["packing"] = new("packing", new[] { "picking" }),
        ["cycle_counts"] = new("cycle_counts", new[] { "inventory" }),
        ["orders"] = new("orders", new[] { "crm" }),
        ["inventory_allocation"] = new("inventory_allocation", new[] { "orders", "inventory" }),
        ["replenishment"] = new("replenishment", new[] { "inventory" }),
        ["pricing"] = new("pricing", new[] { "orders" }),
        ["delivery_orders"] = new("delivery_orders", new[] { "orders", "routes" }),
        ["stops"] = new("stops", new[] { "delivery_orders", "routes" }),
        ["proof_of_delivery"] = new("proof_of_delivery", new[] { "stops" }),
        ["returns"] = new("returns", new[] { "orders" }),
        ["multi_client"] = new("multi_client", new[] { "crm" }),
        ["slas"] = new("slas", new[] { "multi_client" }),
        ["billing"] = new("billing", new[] { "orders" }),
        ["carrier_management"] = new("carrier_management", new[] { "shipments" }),
        ["products"] = new("products", new[] { "inventory" }),
        ["fulfillment"] = new("fulfillment", new[] { "orders", "inventory" }),
        ["customers"] = new("customers", new[] { "crm" }),
        ["projects"] = new("projects", new[] { "crm" }),
        ["work_orders"] = new("work_orders", new[] { "projects" }),
        ["materials"] = new("materials", new[] { "inventory" }),
        ["technicians"] = new("technicians", new[] { "work_orders" }),
        ["scheduling"] = new("scheduling", new[] { "work_orders" }),
        ["service_slas"] = new("service_slas", new[] { "work_orders" }),
        ["traceability"] = new("traceability", new[] { "inventory", "quality" }),
        ["ai_agents"] = new("ai_agents", Array.Empty<string>()),
        ["automations"] = new("automations", Array.Empty<string>())
    };
    public IReadOnlyCollection<ModuleDefinition> Modules => _modules.Values.ToArray();
    public IReadOnlyCollection<string> Resolve(IReadOnlyCollection<string> requestedModules)
    {
        var resolved = new List<string>(); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string code) { if (!visited.Add(code) || !_modules.TryGetValue(code, out var module)) return; foreach (var dependency in module.Dependencies) Add(dependency); resolved.Add(module.Code); }
        foreach (var code in requestedModules) Add(code); return resolved;
    }
}

public interface IModuleProvisioningOrchestrator { Task ProvisionAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default); }
public interface IModuleDeactivationOrchestrator { Task DeactivateAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default); }

public sealed class ModuleProvisioningOrchestrator(IEnumerable<IModuleProvisioner> provisioners, IModuleRegistry registry, AppDbContext dbContext, ITenantLifecycleEventStore events) : IModuleProvisioningOrchestrator
{
    public async Task ProvisionAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default)
    {
        var requested = registry.Resolve(modules); var byCode = provisioners.ToDictionary(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase);
        foreach (var module in requested)
        {
            if (!byCode.TryGetValue(module, out var provisioner)) continue;
            var row = await dbContext.TenantModuleProvisionings.FindAsync(new object[] { tenantId, module }, cancellationToken);
            if (row?.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) == true) continue;
            row ??= new TenantModuleProvisioning { TenantId = tenantId, ModuleCode = module };
            if (dbContext.Entry(row).State == EntityState.Detached) dbContext.TenantModuleProvisionings.Add(row);
            row.Status = "provisioning"; row.AttemptCount++; row.LastAttemptAtUtc = DateTime.UtcNow; row.LastError = null; row.NextRetryAtUtc = null; row.UpdatedAtUtc = DateTime.UtcNow;
            events.Record(new(tenantId, "module", "provisioning", $"Provisioning started for {module}", row.UpdatedAtUtc, new Dictionary<string,string>{{"module",module}}));
            await dbContext.SaveChangesAsync(cancellationToken);
            try { await provisioner.ProvisionAsync(tenantId, cancellationToken); row.Status = "completed"; row.CompletedAtUtc = DateTime.UtcNow; row.UpdatedAtUtc = DateTime.UtcNow; events.Record(new(tenantId, "module", "completed", $"Provisioning completed for {module}", row.UpdatedAtUtc, new Dictionary<string,string>{{"module",module}})); }
            catch (Exception ex) { row.Status = "failed"; row.LastError = ex.ToString(); row.NextRetryAtUtc = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(row.AttemptCount, 6)))); row.UpdatedAtUtc = DateTime.UtcNow; events.Record(new(tenantId, "module", "failed", $"Provisioning failed for {module}", row.UpdatedAtUtc, new Dictionary<string,string>{{"module",module}})); }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class ModuleDeactivationOrchestrator(IEnumerable<IModuleLifecycleHandler> handlers, AppDbContext dbContext, ITenantLifecycleEventStore events) : IModuleDeactivationOrchestrator
{
    public async Task DeactivateAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default)
    {
        var byCode = handlers.ToDictionary(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var row = await dbContext.TenantModuleProvisionings.FindAsync(new object[] { tenantId, module }, cancellationToken);
            if (row is null || row.Status.Equals("deactivated", StringComparison.OrdinalIgnoreCase)) continue;
            try { if (byCode.TryGetValue(module, out var handler)) await handler.DeactivateAsync(tenantId, cancellationToken); row.Status = "deactivated"; row.NextRetryAtUtc = null; row.LastError = null; row.UpdatedAtUtc = DateTime.UtcNow; events.Record(new(tenantId, "module", "deactivated", $"Module deactivated: {module}", row.UpdatedAtUtc, new Dictionary<string,string>{{"module",module}})); }
            catch (Exception ex) { row.Status = "deactivation_failed"; row.LastError = ex.ToString(); row.UpdatedAtUtc = DateTime.UtcNow; events.Record(new(tenantId, "module", "deactivation_failed", $"Module deactivation failed: {module}", row.UpdatedAtUtc, new Dictionary<string,string>{{"module",module}})); }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class GoldenPipelineModuleProvisioner(IGoldenPipelineProvisioner goldenPipeline) : IModuleProvisioner { public string ModuleCode => "golden_pipeline"; public Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default) => goldenPipeline.EnsureProvisionedAsync(tenantId, cancellationToken); }
public sealed class GoldenPipelineModuleLifecycleHandler : IModuleLifecycleHandler { public string ModuleCode => "golden_pipeline"; public Task DeactivateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask; }

public sealed class RegisteredModuleProvisioner(string moduleCode) : IModuleProvisioner
{
    public string ModuleCode => moduleCode;
    public Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
