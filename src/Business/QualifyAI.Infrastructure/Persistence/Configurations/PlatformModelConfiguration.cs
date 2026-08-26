using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

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
    }
}
