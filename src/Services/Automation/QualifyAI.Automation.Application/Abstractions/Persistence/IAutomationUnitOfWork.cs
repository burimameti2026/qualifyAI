namespace QualifyAI.Automation.Application.Abstractions.Persistence;

public interface IAutomationUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
