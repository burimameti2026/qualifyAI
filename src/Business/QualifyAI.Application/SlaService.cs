using QualifyAI.Domain;
namespace QualifyAI.Application;
public sealed class SlaService { public void Apply(Ticket ticket,SlaPolicy policy,DateTime nowUtc){ ticket.SlaPolicyId=policy.Id; ticket.FirstResponseDueUtc=nowUtc.AddMinutes(policy.FirstResponseMinutes); ticket.ResolutionDueUtc=nowUtc.AddMinutes(policy.ResolutionMinutes); } public bool IsBreached(Ticket t,DateTime nowUtc)=> (t.Status!=TicketStatus.Resolved&&t.ResolutionDueUtc<nowUtc); }
