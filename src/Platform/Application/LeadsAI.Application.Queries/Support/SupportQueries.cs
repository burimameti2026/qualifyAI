using MediatR;
using LeadsAI.BuildingBlocks.Application.Security;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.Domain;

namespace LeadsAI.Application.Queries.Support;

[AccessControl(QualifyAiPermissions.ConversationsRead, QualifyAiModules.Inbox)]
public sealed record ListConversationsQuery(Guid TenantId, int Take = 300)
    : IRequest<IReadOnlyList<Conversation>>;

[AccessControl(QualifyAiPermissions.ConversationsRead, QualifyAiModules.Inbox)]
public sealed record ListConversationMessagesQuery(Guid TenantId, Guid ConversationId)
    : IRequest<IReadOnlyList<Message>>;

[AccessControl(QualifyAiPermissions.TicketsRead, QualifyAiModules.Ticketing)]
public sealed record ListTicketsQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Ticket>>;
