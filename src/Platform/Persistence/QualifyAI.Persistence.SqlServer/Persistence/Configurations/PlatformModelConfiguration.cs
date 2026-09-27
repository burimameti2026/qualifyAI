using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Configurations;

internal static class PlatformModelConfiguration
{
    internal static void ConfigurePlatformModel(this ModelBuilder builder)
    {
        builder.Entity<Tenant>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        builder.Entity<AppUser>()
            .HasIndex(x => new { x.TenantId, x.Email })
            .IsUnique();

        builder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(x => new { x.Id, x.Consumer });
            entity.Property(x => x.Consumer).HasMaxLength(200);
            entity.HasIndex(x => x.ProcessedAtUtc);
        });
    }
}
