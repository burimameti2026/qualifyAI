using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed record AutonomousAcquisitionTemplate(
 string Code,
 string Name,
 string Industry,
 string Region,
 string[] Keywords,
 string[] Signals,
 int MinimumScore=90,
 string OfferName="",
 string OfferSummary="",
 string BuyerRoles="",
 string PainPoints="",
 string EmailSubject="",
 string EmailBody="");

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
  new(
   "logistics",
   "Logistics Automation",
   "Logistics & Transport",
   "Europe",
   ["logistics companies","3PL providers","freight forwarders","transport operators","warehouse operators","distribution companies"],
   ["logistics","freight","transport","warehouse","distribution","supply chain","fleet"],
   75,
   "Logistics Process Automation",
   "Software that automates repetitive logistics workflows, reduces manual coordination and gives operations teams one place to manage customer, shipment and exception processes.",
   "Operations Director, Logistics Director, Head of Operations, Supply Chain Director, COO",
   "Manual coordination, repetitive email work, fragmented customer and shipment processes, slow exception handling, poor operational visibility",
   "A practical way to automate repetitive logistics operations",
   "Hi {{contactName}},\n\nI noticed {{companyName}} is operating in {{industry}}. Logistics teams often lose time to repetitive coordination, email follow-ups and manual exception handling.\n\nWe provide logistics software that automates these processes and gives operations teams a single workflow for customer, shipment and exception management.\n\nWould a short conversation be useful to see whether this fits {{companyName}}?\n\nBest regards"),
  new(
   "fleet",
   "Fleet & Transport Automation",
   "Fleet & Mobility",
   "Europe",
   ["fleet management","commercial vehicle fleet","transport fleet","vehicle operations","telematics"],
   ["fleet","vehicles","logistics","transport","mobility"],
   75,
   "Fleet Operations Automation",
   "Software for automating fleet coordination, follow-ups, operational tasks and exception workflows.",
   "Fleet Director, Operations Director, Transport Manager, COO",
   "Manual fleet coordination, vehicle follow-ups, operational exceptions and fragmented workflows",
   "Automate repetitive fleet operations",
   "Hi {{contactName}},\n\nI noticed {{companyName}} operates in fleet and transport. We help operations teams automate repetitive coordination, follow-ups and exception workflows.\n\nWould it be useful to compare the current process with what can be automated?\n\nBest regards"),
  new("saas","SaaS","Software as a Service","Europe",["B2B SaaS companies","software startups","cloud software companies"],["saas","software","cloud","platform","subscription"]),
  new("software","Software Companies","Software","Europe",["software development companies","enterprise software companies","business software companies"],["software","platform","technology","enterprise"]),
  new("custom","Custom ICP","Custom","Europe",[],[])
 };

 public IReadOnlyList<AutonomousAcquisitionTemplate> List()=>Templates;

 public AutonomousAcquisitionTemplate Resolve(string code)=>
  Templates.FirstOrDefault(x=>string.Equals(x.Code,code,StringComparison.OrdinalIgnoreCase))??Templates.First(x=>x.Code=="custom");

 public AutonomousAcquisitionTemplate Apply(AutonomousAcquisitionAgent agent)
 {
  var t=Resolve(agent.TemplateCode);
  agent.TemplateCode=t.Code;
  if(string.IsNullOrWhiteSpace(agent.Industry))agent.Industry=t.Industry;
  if(string.IsNullOrWhiteSpace(agent.Region))agent.Region=t.Region;
  if(agent.MinimumScore<=0)agent.MinimumScore=t.MinimumScore;
  if(string.IsNullOrWhiteSpace(agent.IcpJson))
   agent.IcpJson=System.Text.Json.JsonSerializer.Serialize(new { t.OfferName,t.OfferSummary,t.BuyerRoles,t.PainPoints });
  return t;
 }
}
