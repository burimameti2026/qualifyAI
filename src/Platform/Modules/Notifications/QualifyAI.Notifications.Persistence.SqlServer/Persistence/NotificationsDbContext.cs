using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Entitlements;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Notifications.Domain.Notifications;

namespace QualifyAI.Notifications.Persistence.SqlServer;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TenantEntitlementState> TenantEntitlements => Set<TenantEntitlementState>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.Entity<TenantEntitlementState>(entity =>
        {
            entity.ToTable("TenantEntitlements");
            entity.HasKey(x => x.TenantId);
            entity.HasIndex(x => x.TenantSlug);
            entity.Property(x => x.TenantSlug).HasMaxLength(128);
            entity.Property(x => x.TenantStatus).HasMaxLength(32);
            entity.Property(x => x.LicensePlan).HasMaxLength(64);
            entity.Property(x => x.LicenseStatus).HasMaxLength(32);
        });
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(x => new { x.Id, x.Consumer });
            entity.Property(x => x.Consumer).HasMaxLength(200);
        });
        base.OnModelCreating(modelBuilder);
    }
}
