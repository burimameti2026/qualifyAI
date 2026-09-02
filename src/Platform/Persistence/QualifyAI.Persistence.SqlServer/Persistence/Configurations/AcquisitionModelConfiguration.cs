using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Configurations;

internal static class AcquisitionModelConfiguration
{
    internal static void ConfigureAcquisitionModel(this ModelBuilder builder)
    {
        builder.Entity<IcpProfile>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Industry).HasMaxLength(120);
            b.Property(x => x.CountriesCsv).HasMaxLength(500);
            b.Property(x => x.IntentKeywordsCsv).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.Active });
        });
        builder.Entity<Prospect>(b =>
        {
            b.Property(x => x.CompanyName).HasMaxLength(250);
            b.Property(x => x.Domain).HasMaxLength(253);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.JobTitle).HasMaxLength(160);
            b.Property(x => x.Industry).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(100);
            b.Property(x => x.Source).HasMaxLength(80);
            b.Property(x => x.Priority).HasMaxLength(20);
            b.Property(x => x.ContactReadiness).HasMaxLength(80);
            b.Property(x => x.SuggestedBuyer).HasMaxLength(200);
            b.Property(x => x.SizeBand).HasMaxLength(80);
            b.Property(x => x.Offer).HasMaxLength(500);
            b.Property(x => x.SourceUrl).HasMaxLength(2000);
            b.Property(x => x.VerificationStatus).HasMaxLength(500);
            b.Property(x => x.OutreachStatus).HasMaxLength(80);
            b.Property(x => x.DatasetOrigin).HasMaxLength(200);
            b.Ignore(x => x.PriorityScore);
            b.HasIndex(x => new { x.TenantId, x.Domain }).IsUnique().HasFilter("[Domain] <> N''");
            b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique().HasFilter("[Email] <> N''");
            b.HasIndex(x => new { x.TenantId, x.Status, x.FitScore, x.IntentScore });
        });
        builder.Entity<ProspectSignal>(b =>
        {
            b.Property(x => x.Type).HasMaxLength(100);
            b.Property(x => x.Source).HasMaxLength(100);
            b.Property(x => x.SourceUrl).HasMaxLength(2000);
            b.HasIndex(x => new { x.TenantId, x.ProspectId, x.ObservedAtUtc });
        });
        builder.Entity<TargetList>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.Name });
        });
        builder.Entity<TargetListMember>(b => b.HasIndex(x => new { x.TenantId, x.TargetListId, x.ProspectId }).IsUnique());
        builder.Entity<Campaign>(b => b.HasIndex(x => new { x.TenantId, x.Status, x.StartsAtUtc }));
        builder.Entity<CampaignStep>(b => b.HasIndex(x => new { x.TenantId, x.CampaignId, x.StepNumber }).IsUnique());
        builder.Entity<CampaignRecipient>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.CampaignId, x.ProspectId }).IsUnique();
            b.HasIndex(x => new { x.Status, x.NextRunAtUtc });
        });
        builder.Entity<OutreachMessage>(b => b.HasIndex(x => new { x.TenantId, x.CampaignId, x.ProspectId }));
        builder.Entity<ProspectReply>(b => b.HasIndex(x => new { x.TenantId, x.CampaignId, x.ReceivedAtUtc }));
    }
}
