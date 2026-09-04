using System.Text.Json;
using QualifyAI.Domain;
namespace QualifyAI.Application;
public sealed class BillingService { public bool HasEntitlement(Plan plan,string key){ using var d=JsonDocument.Parse(string.IsNullOrWhiteSpace(plan.EntitlementsJson)?"{}":plan.EntitlementsJson); return d.RootElement.TryGetProperty(key,out var v) && (v.ValueKind==JsonValueKind.True || (v.ValueKind==JsonValueKind.Number&&v.GetInt32()>0)); } }
