using LeadsAI.Domain;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed record AutonomousAcquisitionMessageTemplate(int Step,string Name,string Subject,string Body,int DelayHours=0,bool RequiresApproval=true);

public sealed record AutonomousAcquisitionTemplate(
 string Code,string Name,string Industry,string Region,string[] Keywords,string[] Signals,int MinimumScore=90,
 string Description="",string ProspectType="Company",string TargetDefinition="",AutonomousAcquisitionMessageTemplate[]? Messages=null)
{
 public string UseCaseId=>Code;
 public IReadOnlyList<AutonomousAcquisitionMessageTemplate> OutreachTemplates=>Messages??Array.Empty<AutonomousAcquisitionMessageTemplate>();
}

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
  new("fleet","Fleet Management — FusionFleet","Fleet & Mobility","Europe",["fleet management","commercial vehicle fleet","vehicle operations","transport fleet","telematics"],["fleet","vehicles","logistics","transport","mobility"],90,"Find fleet and transport companies where fleet visibility and operational coordination create manual workload.","Logistics / fleet company","Companies operating commercial fleets, carriers or transport operations.",[
   new(1,"Intro","Quick question about {{company}}","Hi {{contact}},\n\nI noticed {{company}} operates in {{industry}}. I wanted to ask how your team currently handles fleet visibility and operational coordination.\n\nWould it be useful to compare how similar teams manage this today?",0,true),
   new(2,"Follow-up","Following up on {{company}}","Hi {{contact}},\n\nFollowing up on my note about fleet and operations at {{company}}. If this is relevant, I can share a short overview of how teams reduce manual coordination and customer updates.",48,true),
   new(3,"Close","Should I close the loop?","Hi {{contact}},\n\nI don't want to keep filling your inbox. If improving fleet or operational coordination is on your roadmap, I'm happy to send a concise example. Otherwise I'll close the loop.",96,true)]),
  new("logistics","Logistics Europe","Logistics & Transport","Europe",["logistics companies","transport operators","freight companies","supply chain companies"],["logistics","freight","transport","warehouse","supply chain"],80,"Find logistics and 3PL companies with operational coordination, shipment visibility or customer communication workload.","Logistics / 3PL company","Companies providing logistics, 3PL, freight forwarding, transport or fulfillment services.",[
   new(1,"Intro","Quick question about {{company}}","Hi {{contact}},\n\nI came across {{company}} and wanted to understand how your team handles shipment coordination and customer updates today.\n\nWould a short comparison with similar logistics teams be useful?",0,true),
   new(2,"Follow-up","Re: {{company}}","Hi {{contact}},\n\nJust following up. We work around operational coordination, shipment visibility and reducing manual customer updates. Happy to share a short example if this is relevant.",72,true),
   new(3,"Close","Closing the loop","Hi {{contact}},\n\nI'll close the loop here. If operational automation becomes a priority at {{company}}, I'd be happy to reconnect.",120,true)]),
  new("custom","Custom ICP","Custom","Europe",[],[],70,"AI-assisted custom prospecting package. The campaign ICP supplies the missing targeting details.","Company","Defined by the campaign ICP.",[
   new(1,"Intro","Question for {{company}}","Hi {{contact}},\n\nI wanted to ask a quick question about how {{company}} currently handles its operational workflow. Would a short comparison be useful?",0,true)])
 };
 public IReadOnlyList<AutonomousAcquisitionTemplate> List()=>Templates;
 public AutonomousAcquisitionTemplate Resolve(string code)=>Templates.FirstOrDefault(x=>string.Equals(x.Code,code,StringComparison.OrdinalIgnoreCase))??Templates[^1];
 public AutonomousAcquisitionTemplate Apply(AutonomousAcquisitionAgent agent){var t=Resolve(agent.TemplateCode);if(string.IsNullOrWhiteSpace(agent.Industry))agent.Industry=t.Industry;if(string.IsNullOrWhiteSpace(agent.Region))agent.Region=t.Region;if(agent.MinimumScore<=0)agent.MinimumScore=t.MinimumScore;return t;}
}
