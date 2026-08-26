using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QualifyAI.Knowledge.Domain.KnowledgeBases;
namespace QualifyAI.Knowledge.Infrastructure.Persistence.Configurations;
public sealed class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    public void Configure(EntityTypeBuilder<KnowledgeBase> b)
    {
        b.ToTable("KnowledgeBases");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}
