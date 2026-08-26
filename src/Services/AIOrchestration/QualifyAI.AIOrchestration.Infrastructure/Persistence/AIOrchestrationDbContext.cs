using Microsoft.EntityFrameworkCore;
using QualifyAI.AIOrchestration.Domain.Agents;
namespace QualifyAI.AIOrchestration.Infrastructure.Persistence;
public sealed class AIOrchestrationDbContext(DbContextOptions<AIOrchestrationDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents => Set<Agent>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIOrchestrationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
