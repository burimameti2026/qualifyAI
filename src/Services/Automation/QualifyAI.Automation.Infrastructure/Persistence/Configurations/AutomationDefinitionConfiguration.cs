using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QualifyAI.Automation.Domain.AutomationDefinitions;
namespace QualifyAI.Automation.Infrastructure.Persistence.Configurations;
public sealed class AutomationDefinitionConfiguration : IEntityTypeConfiguration<AutomationDefinition>
{
    public void Configure(EntityTypeBuilder<AutomationDefinition> b)
    {
        b.ToTable("AutomationDefinitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}
