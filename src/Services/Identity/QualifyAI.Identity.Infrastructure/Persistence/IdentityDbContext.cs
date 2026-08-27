using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Identity.Domain.AccessControl;
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
    public DbSet<AccessRole> AccessRoles => Set<AccessRole>();
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();
    public DbSet<RolePermissionGrant> RolePermissionGrants => Set<RolePermissionGrant>();
    public DbSet<ClientPermissionGrant> ClientPermissionGrants => Set<ClientPermissionGrant>();
    public DbSet<SecurityAuditEntry> SecurityAuditEntries => Set<SecurityAuditEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        ConfigureIdentity(builder);
        ConfigureAccessControl(builder);
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

    private static void ConfigureAccessControl(ModelBuilder builder)
    {
        builder.Entity<AccessRole>(b =>
        {
            b.ToTable("AccessRoles");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Scope).HasConversion<string>().HasMaxLength(32);
        });

        builder.Entity<PermissionDefinition>(b =>
        {
            b.ToTable("PermissionDefinitions");
            b.HasKey(x => x.Code);
            b.Property(x => x.Code).HasMaxLength(200);
            b.Property(x => x.Module).HasMaxLength(100).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
        });

        builder.Entity<RolePermissionGrant>(b =>
        {
            b.ToTable("RolePermissionGrants");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.RoleId, x.Permission }).IsUnique();
            b.Property(x => x.Permission).HasMaxLength(200).IsRequired();
            b.HasOne<AccessRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClientPermissionGrant>(b =>
        {
            b.ToTable("ClientPermissionGrants");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ClientApplicationId, x.Permission }).IsUnique();
            b.Property(x => x.Permission).HasMaxLength(200).IsRequired();
            b.HasOne<ClientApplication>().WithMany().HasForeignKey(x => x.ClientApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SecurityAuditEntry>(b =>
        {
            b.ToTable("SecurityAuditEntries");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
            b.Property(x => x.Action).HasMaxLength(150).IsRequired();
            b.Property(x => x.TargetType).HasMaxLength(100).IsRequired();
            b.Property(x => x.TargetId).HasMaxLength(200).IsRequired();
            b.Property(x => x.DetailsJson).IsRequired();
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
            b.HasMany(x => x.Modules).WithOne().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
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
            b.HasMany(x => x.Scopes).WithOne().HasForeignKey(x => x.ClientApplicationId).OnDelete(DeleteBehavior.Cascade);
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
