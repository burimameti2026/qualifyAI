namespace QualifyAI.Automation.Domain.AutomationDefinitions;
public interface IAutomationDefinitionRepository
{
    Task<AutomationDefinition?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(AutomationDefinition entity, CancellationToken ct = default);
}
