using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Configurations;

internal static class CrmModelConfiguration
{
    internal static void ConfigureCrmModel(this ModelBuilder builder)
    {
        builder.Entity<Lead>()
            .HasIndex(x => new { x.TenantId, x.Score });
    }
}
