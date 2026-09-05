using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer.Configurations;
using QualifyAI.Persistence.SqlServer.Projections;
namespace QualifyAI.Persistence.SqlServer;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options):DbContext(options)
{
 public DbSet<AutonomousAcquisitionAgent> AutonomousAcquisitionAgents=>Set<AutonomousAcquisitionAgent>();
 public DbSet<AutonomousAcquisitionAgentRun> AutonomousAcquisitionAgentRuns=>Set<AutonomousAcquisitionAgentRun>();
 public DbSet<AutonomousAcquisitionAgentMemory> AutonomousAcquisitionAgentMemories=>Set<AutonomousAcquisitionAgentMemory>();
 public DbSet<Prospect> Prospects=>Set<Prospect>(); public DbSet<ProspectSignal> ProspectSignals=>Set<ProspectSignal>(); public DbSet<IcpProfile> IcpProfiles=>Set<IcpProfile>();
 protected override void OnModelCreating(ModelBuilder builder){base.OnModelCreating(builder);builder.ConfigureBusinessEntityKeys();builder.ConfigurePlatformModel();builder.ConfigureCrmModel();builder.ConfigureAcquisitionModel();builder.ConfigureAutonomousAcquisitionModel();builder.Entity<AutonomousAcquisitionAgentMemory>(e=>{e.ToTable("AutonomousAcquisitionAgentMemories");e.HasKey(x=>x.Id);e.Property(x=>x.Key).HasMaxLength(256).IsRequired();e.Property(x=>x.Category).HasMaxLength(64).IsRequired();e.Property(x=>x.Value).HasMaxLength(8000).IsRequired();e.HasIndex(x=>new{x.TenantId,x.AgentId,x.Category,x.Key}).IsUnique();});}
}
