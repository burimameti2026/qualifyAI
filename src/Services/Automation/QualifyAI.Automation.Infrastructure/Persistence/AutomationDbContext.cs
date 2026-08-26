using Microsoft.EntityFrameworkCore;
using QualifyAI.Automation.Domain.AutomationDefinitions;
namespace QualifyAI.Automation.Infrastructure.Persistence;
public sealed class AutomationDbContext(DbContextOptions<AutomationDbContext> options) : DbContext(options)
{
    public DbSet<AutomationDefinition> AutomationDefinitions => Set<AutomationDefinition>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutomationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
