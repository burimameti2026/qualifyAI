using Microsoft.EntityFrameworkCore;
using QualifyAI.Knowledge.Domain.KnowledgeBases;
namespace QualifyAI.Knowledge.Infrastructure.Persistence;
public sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : DbContext(options)
{
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
