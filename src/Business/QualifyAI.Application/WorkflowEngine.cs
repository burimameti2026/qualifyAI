using System.Text.Json;
using QualifyAI.Domain;
namespace QualifyAI.Application;
public record WorkflowExecutionContext(Guid TenantId, Guid? LeadId, Guid? ContactId, Dictionary<string,string> Facts);
public record WorkflowExecutionResult(string? NextNodeKey,int ScoreDelta,List<string> Actions);
public sealed class WorkflowEngine {
 public WorkflowExecutionResult Execute(WorkflowNode node,IEnumerable<WorkflowEdge> edges,WorkflowExecutionContext ctx){
  var actions=new List<string>(); int delta=0;
  using var doc=JsonDocument.Parse(string.IsNullOrWhiteSpace(node.ConfigJson)?"{}":node.ConfigJson);
  if(node.Type.Equals("score",StringComparison.OrdinalIgnoreCase) && doc.RootElement.TryGetProperty("points",out var p)) delta=p.GetInt32();
  if(node.Type is "assign" or "email" or "webhook" or "createLead" or "createTicket" or "bookMeeting") actions.Add(node.Type);
  var next=edges.FirstOrDefault(e=>e.FromNodeKey==node.NodeKey)?.ToNodeKey;
  return new(next,delta,actions);
 }
}
