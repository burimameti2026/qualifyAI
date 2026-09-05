namespace QualifyAI.Infrastructure;

public sealed record ModuleDefinition(string Code, IReadOnlyCollection<string> Dependencies);

public interface IModuleProvisioner
{
    string ModuleCode { get; }
    Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IModuleRegistry
{
    IReadOnlyCollection<ModuleDefinition> Modules { get; }
    IReadOnlyCollection<string> Resolve(IReadOnlyCollection<string> requestedModules);
}

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
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string code)
        {
            if (!_modules.TryGetValue(code, out var module)) return;
            foreach (var dependency in module.Dependencies) Add(dependency);
            resolved.Add(module.Code);
        }
        foreach (var code in requestedModules) Add(code);
        return resolved.ToArray();
    }
}

public interface IModuleProvisioningOrchestrator
{
    Task ProvisionAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default);
}

public sealed class ModuleProvisioningOrchestrator(IEnumerable<IModuleProvisioner> provisioners, IModuleRegistry registry) : IModuleProvisioningOrchestrator
{
    public async Task ProvisionAsync(Guid tenantId, IReadOnlyCollection<string> modules, CancellationToken cancellationToken = default)
    {
        var requested = registry.Resolve(modules);
        var byCode = provisioners.ToDictionary(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase);
        foreach (var module in requested)
        {
            if (byCode.TryGetValue(module, out var provisioner))
                await provisioner.ProvisionAsync(tenantId, cancellationToken);
        }
    }
}

public sealed class GoldenPipelineModuleProvisioner(IGoldenPipelineProvisioner goldenPipeline) : IModuleProvisioner
{
    public string ModuleCode => "golden_pipeline";
    public async Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await goldenPipeline.EnsureProvisionedAsync(tenantId, cancellationToken);
}
