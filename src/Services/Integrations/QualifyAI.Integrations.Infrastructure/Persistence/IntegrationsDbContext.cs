using Microsoft.EntityFrameworkCore;
using QualifyAI.Integrations.Domain.Integrations;
namespace QualifyAI.Integrations.Infrastructure.Persistence;
public sealed class IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : DbContext(options)
{
    public DbSet<Integration> Integrations => Set<Integration>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntegrationsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
