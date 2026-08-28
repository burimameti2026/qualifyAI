using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Configurations;

internal static class CrmModelConfiguration
{
    internal static void ConfigureCrmModel(this ModelBuilder builder)
    {
        builder.Entity<Company>().HasIndex(x => new { x.TenantId, x.Domain });
        builder.Entity<Contact>().HasIndex(x => new { x.TenantId, x.Email });
        builder.Entity<Lead>().HasIndex(x => new { x.TenantId, x.Score });
        builder.Entity<Opportunity>().HasIndex(x => new { x.TenantId, x.Status, x.PipelineStageId });
        builder.Entity<Opportunity>().Property(x => x.LossReason).HasMaxLength(1000);
        builder.Entity<Pipeline>().HasIndex(x => new { x.TenantId, x.IsDefault });
        builder.Entity<PipelineStage>().HasIndex(x => new { x.TenantId, x.PipelineId, x.SortOrder }).IsUnique();
        builder.Entity<PipelineStage>().Property(x => x.Probability).HasPrecision(5, 2);
    }
}
