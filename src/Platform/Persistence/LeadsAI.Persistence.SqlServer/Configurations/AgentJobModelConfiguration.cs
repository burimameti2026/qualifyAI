using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;

namespace LeadsAI.Persistence.SqlServer.Configurations;

public static class AgentJobModelConfiguration
{
    public static void ConfigureAgentJobModel(this ModelBuilder builder)
    {
        builder.Entity<AgentJob>(e =>
        {
            e.ToTable("AgentJobs");
            e.HasKey(x => x.Id);

            e.Property(x => x.Type).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            e.Property(x => x.ResultJson);
            e.Property(x => x.Error).HasMaxLength(4000);
            e.Property(x => x.WorkerId).HasMaxLength(128);

            // The queue is tenant-owned. This index is the hot path for GetNextJobAndClaimAsync.
            e.HasIndex(x => new { x.TenantId, x.Status, x.ScheduledAtUtc, x.Priority });

            // Operational lookups.
            e.HasIndex(x => new { x.TenantId, x.CampaignId, x.ContainerId });
            e.HasIndex(x => new { x.TenantId, x.AgentId, x.Status });
            e.HasIndex(x => x.LeaseUntilUtc);
        });
    }
}
