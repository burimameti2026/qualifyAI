using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Persistence.SqlServer.Projections;

namespace QualifyAI.Infrastructure;

public sealed record ModuleDefinition(string Code, IReadOnlyCollection<string> Dependencies);
public interface IModuleProvisioner { string ModuleCode { get; } Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default); }
public interface IModuleRegistry { IReadOnlyCollection<ModuleDefinition> Modules { get; } IReadOnlyCollection<string> Resolve(IReadOnlyCollection<string> requestedModules); }

public sealed class ModuleRegistry(IEnumerable<IModuleProvisioner> provisioners) : IModuleRegistry
{
    private readonly Dictionary<string, ModuleDefinition> _modules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crm"] = new("crm", Array.Empty<string>()),
        ["golden_pipeline"] = new("golden_pipeline", new[] { "crm" })
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

public sealed class ModuleProvisioningOrchestrator(IEnumerable<IModuleProvisioner> provisioners, IModuleRegistry registry, AppDbContext dbContext) : IModuleProvisioningOrchestrator
{
    public async Task ProvisionAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default)
    {
        var requested = registry.Resolve(modules);
        var byCode = provisioners.ToDictionary(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase);
        foreach (var module in requested)
        {
            if (!byCode.TryGetValue(module, out var provisioner)) continue;
            var row = await dbContext.TenantModuleProvisionings.FindAsync(new object[] { tenantId, module }, cancellationToken);
            if (row?.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) == true) continue;
            row ??= new TenantModuleProvisioning { TenantId = tenantId, ModuleCode = module };
            if (dbContext.Entry(row).State == EntityState.Detached) dbContext.TenantModuleProvisionings.Add(row);
            row.Status = "provisioning"; row.AttemptCount++; row.LastAttemptAtUtc = DateTime.UtcNow; row.LastError = null; row.NextRetryAtUtc = null; row.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            try
            {
                await provisioner.ProvisionAsync(tenantId, cancellationToken);
                row.Status = "completed"; row.CompletedAtUtc = DateTime.UtcNow; row.UpdatedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                row.Status = "failed"; row.LastError = ex.ToString(); row.NextRetryAtUtc = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(row.AttemptCount, 6)))); row.UpdatedAtUtc = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class GoldenPipelineModuleProvisioner(IGoldenPipelineProvisioner goldenPipeline) : IModuleProvisioner
{
    public string ModuleCode => "golden_pipeline";
    public Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default) => goldenPipeline.EnsureProvisionedAsync(tenantId, cancellationToken);
}
