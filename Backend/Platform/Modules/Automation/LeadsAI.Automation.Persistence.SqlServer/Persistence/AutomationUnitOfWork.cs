using LeadsAI.Automation.Application.Abstractions.Persistence;

namespace LeadsAI.Automation.Persistence.SqlServer;

public sealed class AutomationUnitOfWork(AutomationDbContext dbContext) : IAutomationUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
