using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Identity.Domain.Clients;
using QualifyAI.Identity.Domain.Licensing;
using QualifyAI.Identity.Domain.Tenants;
using QualifyAI.Identity.Infrastructure.Identity;

namespace QualifyAI.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicenseModule> LicenseModules => Set<LicenseModule>();
    public DbSet<ClientApplication> ClientApplications => Set<ClientApplication>();
    public DbSet<ClientScope> ClientScopes => Set<ClientScope>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        ConfigureIdentity(builder);
        ConfigureTenants(builder);
        ConfigureLicensing(builder);
        ConfigureClients(builder);
        ConfigureOutbox(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users");
            b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique();
            b.Property(x => x.TenantSlug).HasMaxLength(100).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100);
            b.Property(x => x.LastName).HasMaxLength(100);
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("Roles");
            b.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
        });

        builder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.UserId, x.Permission }).IsUnique();
            b.Property(x => x.Permission).HasMaxLength(200).IsRequired();
        });
    }

    private static void ConfigureTenants(ModelBuilder builder)
    {
        builder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Slug).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.Property(x => x.ContactEmail).HasMaxLength(320).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });
    }

    private static void ConfigureLicensing(ModelBuilder builder)
    {
        builder.Entity<License>(b =>
        {
            b.ToTable("Licenses");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TenantId).IsUnique();
            b.Property(x => x.Plan).HasMaxLength(100).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasMany(x => x.Modules)
                .WithOne()
                .HasForeignKey(x => x.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Modules).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<LicenseModule>(b =>
        {
            b.ToTable("LicenseModules");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.LicenseId, x.Code }).IsUnique();
            b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        });
    }

    private static void ConfigureClients(ModelBuilder builder)
    {
        builder.Entity<ClientApplication>(b =>
        {
            b.ToTable("ClientApplications");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.ClientId).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasMany(x => x.Scopes)
                .WithOne()
                .HasForeignKey(x => x.ClientApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Scopes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ClientScope>(b =>
        {
            b.ToTable("ClientScopes");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ClientApplicationId, x.Name }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
    }

    private static void ConfigureOutbox(ModelBuilder builder)
    {
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc, x.OccurredAtUtc });
            b.Property(x => x.Type).HasMaxLength(1000).IsRequired();
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.Error).HasMaxLength(4000);
        });
    }
}
