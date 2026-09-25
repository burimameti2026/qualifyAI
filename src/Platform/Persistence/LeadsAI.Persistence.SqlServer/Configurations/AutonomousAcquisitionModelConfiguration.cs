using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;

namespace LeadsAI.Persistence.SqlServer.Configurations;
public static class AutonomousAcquisitionModelConfiguration
{
 public static void ConfigureAutonomousAcquisitionModel(this ModelBuilder builder)
 {
  builder.Entity<AutonomousAcquisitionAgent>(e=>{e.ToTable("AutonomousAcquisitionAgents");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(200).IsRequired();e.Property(x=>x.TemplateCode).HasMaxLength(100).IsRequired();e.Property(x=>x.Industry).HasMaxLength(200).IsRequired();e.Property(x=>x.Region).HasMaxLength(100).IsRequired();e.Property(x=>x.CountriesJson).IsRequired();e.Property(x=>x.IcpJson).IsRequired();e.HasIndex(x=>new{x.TenantId,x.Status});});
  builder.Entity<AutonomousAcquisitionTask>(e=>{e.ToTable("AutonomousAcquisitionTasks");e.HasKey(x=>x.Id);e.Property(x=>x.Type).HasMaxLength(64).IsRequired();e.Property(x=>x.Name).HasMaxLength(200).IsRequired();e.Property(x=>x.ConfigurationJson).IsRequired();e.Property(x=>x.ResultJson).IsRequired();e.Property(x=>x.Error).HasMaxLength(4000);e.HasIndex(x=>new{x.TenantId,x.AgentId,x.RunId,x.Sequence}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.Status});});
  builder.Entity<AutonomousAcquisitionAgentRun>(e=>{e.ToTable("AutonomousAcquisitionAgentRuns");e.HasKey(x=>x.Id);e.Property(x=>x.Query).HasMaxLength(2000);e.Property(x=>x.Error).HasMaxLength(4000);e.HasIndex(x=>new{x.AgentId,x.ScheduledAtUtc});e.HasIndex(x=>new{x.TenantId,x.Status});});
 }
}
