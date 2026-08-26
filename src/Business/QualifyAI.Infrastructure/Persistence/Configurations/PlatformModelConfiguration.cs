using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Persistence.Projections;

namespace QualifyAI.Infrastructure.Persistence.Configurations;

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

        builder.Entity<TenantEntitlementProjection>(entity =>
        {
            entity.ToTable("TenantEntitlementProjections");
            entity.HasKey(x => x.TenantId);
            entity.HasIndex(x => x.TenantSlug).IsUnique();
            entity.Property(x => x.TenantSlug).HasMaxLength(128);
            entity.Property(x => x.TenantStatus).HasMaxLength(32);
            entity.Property(x => x.LicensePlan).HasMaxLength(64);
            entity.Property(x => x.LicenseStatus).HasMaxLength(32);
            entity.Property(x => x.ModulesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.LimitsJson).HasColumnType("nvarchar(max)");
        });
    }
}
