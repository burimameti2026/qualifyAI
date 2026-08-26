using MediatR;
using QualifyAI.Domain;

namespace QualifyAI.Application.Queries.Support;

public sealed record ListConversationsQuery(Guid TenantId, int Take = 300)
    : IRequest<IReadOnlyList<Conversation>>;

public sealed record ListConversationMessagesQuery(Guid TenantId, Guid ConversationId)
    : IRequest<IReadOnlyList<Message>>;

public sealed record ListTicketsQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Ticket>>;
