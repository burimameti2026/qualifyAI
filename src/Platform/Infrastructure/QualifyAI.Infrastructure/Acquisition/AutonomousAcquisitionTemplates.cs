using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed record AutonomousAcquisitionTemplate(string Code,string Name,string Industry,string Region,string[] Keywords,string[] Signals,int MinimumScore=90);

public interface IAutonomousAcquisitionTemplateRegistry
{
 IReadOnlyList<AutonomousAcquisitionTemplate> List();
 AutonomousAcquisitionTemplate Resolve(string code);
 AutonomousAcquisitionTemplate Apply(AutonomousAcquisitionAgent agent);
}

public sealed class AutonomousAcquisitionTemplateRegistry : IAutonomousAcquisitionTemplateRegistry
{
 private static readonly AutonomousAcquisitionTemplate[] Templates =
 {
  new("fleet","Fleet Europe","Fleet & Mobility","Europe",["fleet management","commercial vehicle fleet","vehicle operations","transport fleet","telematics"],["fleet","vehicles","logistics","transport","mobility"]),
  new("logistics","Logistics Europe","Logistics & Transport","Europe",["logistics companies","transport operators","freight companies","supply chain companies"],["logistics","freight","transport","warehouse","supply chain"]),
  new("saas","SaaS","Software as a Service","Europe",["B2B SaaS companies","software startups","cloud software companies"],["saas","software","cloud","platform","subscription"]),
  new("software","Software Companies","Software","Europe",["software development companies","enterprise software companies","business software companies"],["software","platform","technology","enterprise"]),
  new("custom","Custom ICP","Custom","Europe",[],[])
 };
 public IReadOnlyList<AutonomousAcquisitionTemplate> List()=>Templates;
 public AutonomousAcquisitionTemplate Resolve(string code)=>Templates.FirstOrDefault(x=>string.Equals(x.Code,code,StringComparison.OrdinalIgnoreCase))??Templates[^1];
 public AutonomousAcquisitionTemplate Apply(AutonomousAcquisitionAgent agent){var t=Resolve(agent.TemplateCode);if(string.IsNullOrWhiteSpace(agent.Industry))agent.Industry=t.Industry;if(string.IsNullOrWhiteSpace(agent.Region))agent.Region=t.Region;if(agent.MinimumScore<=0)agent.MinimumScore=t.MinimumScore;return t;}
}
