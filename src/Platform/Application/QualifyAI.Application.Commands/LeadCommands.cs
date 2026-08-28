using MediatR;
using QualifyAI.Domain;

namespace QualifyAI.Application.Commands;

public sealed record CreateContactCommand(
    Guid TenantId, Guid? CompanyId, string FirstName, string LastName,
    string Email, string Phone, string LifecycleStage) : IRequest<Contact>;

public sealed record CreateLeadCommand(
    Guid TenantId, Guid ContactId, Guid? CompanyId, string Source,
    int Score, decimal? EstimatedValue, string IntentSummary) : IRequest<Lead>;

public sealed record QualifyLeadCommand(Guid TenantId, Guid LeadId) : IRequest<Lead?>;

public sealed record CreateTicketCommand(
    Guid TenantId, Guid? ConversationId, Guid? ContactId, string Subject,
    string Description, TicketPriority Priority, Guid? SlaPolicyId) : IRequest<Ticket>;
