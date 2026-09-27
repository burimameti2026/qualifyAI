using QualifyAI.Domain;
namespace QualifyAI.Application;
public sealed class LeadQualificationService {
 public (int score,LeadTemperature temperature,List<(string factor,int points,string reason)> reasons) Score(string text){
  var t=(text??"").ToLowerInvariant(); int score=15; var r=new List<(string,int,string)>();
  void Add(string f,int p,string reason){score+=p;r.Add((f,p,reason));}
  if(t.Contains("budget")||t.Contains("€")||t.Contains("eur")) Add("Budget",20,"Budget or price intent detected");
  if(t.Contains("demo")||t.Contains("meeting")||t.Contains("call")) Add("High intent",25,"Visitor requested a demo/meeting");
  if(t.Contains("month")||t.Contains("week")||t.Contains("urgent")) Add("Timeline",15,"Near-term timeline signal");
  if(t.Contains("company")||t.Contains("employees")||t.Contains("trucks")||t.Contains("warehouse")) Add("Firmographic",15,"Company-size/domain information detected");
  if(t.Contains("decision")||t.Contains("owner")||t.Contains("director")) Add("Authority",15,"Decision authority signal");
  score=Math.Clamp(score,0,100); return(score,score>=80?LeadTemperature.Hot:score>=50?LeadTemperature.Warm:LeadTemperature.Cold,r);
 }
}
