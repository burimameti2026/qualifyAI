using MediatR;
using LeadsAI.Application.Abstractions.Persistence;
using LeadsAI.Application.Queries.Support;
using LeadsAI.Domain;

namespace LeadsAI.Persistence.SqlServer.Queries;

public sealed class ListConversationsQueryHandler(ISupportRepository support)
    : IRequestHandler<ListConversationsQuery, IReadOnlyList<Conversation>>
{
    public Task<IReadOnlyList<Conversation>> Handle(ListConversationsQuery request, CancellationToken cancellationToken)
        => support.ListConversationsAsync(request.TenantId, request.Take, cancellationToken);
}

public sealed class ListConversationMessagesQueryHandler(ISupportRepository support)
    : IRequestHandler<ListConversationMessagesQuery, IReadOnlyList<Message>>
{
    public Task<IReadOnlyList<Message>> Handle(ListConversationMessagesQuery request, CancellationToken cancellationToken)
        => support.ListMessagesAsync(request.TenantId, request.ConversationId, cancellationToken);
}

public sealed class ListTicketsQueryHandler(ISupportRepository support)
    : IRequestHandler<ListTicketsQuery, IReadOnlyList<Ticket>>
{
    public Task<IReadOnlyList<Ticket>> Handle(ListTicketsQuery request, CancellationToken cancellationToken)
        => support.ListTicketsAsync(request.TenantId, cancellationToken);
}
