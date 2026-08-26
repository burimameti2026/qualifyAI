using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QualifyAI.AIOrchestration.Domain.Agents;
namespace QualifyAI.AIOrchestration.Infrastructure.Persistence.Configurations;
public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> b)
    {
        b.ToTable("Agents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}
