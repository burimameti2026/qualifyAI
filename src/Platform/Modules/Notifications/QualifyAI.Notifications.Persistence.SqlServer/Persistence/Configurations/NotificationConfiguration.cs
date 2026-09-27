using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QualifyAI.Notifications.Domain.Notifications;
namespace QualifyAI.Notifications.Persistence.SqlServer.Configurations;
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}
