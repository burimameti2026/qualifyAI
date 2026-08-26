using QualifyAI.Domain;

namespace QualifyAI.Application.Abstractions.Persistence;

public interface ISupportRepository
{
    Task<IReadOnlyList<Conversation>> ListConversationsAsync(Guid tenantId, int take = 300, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> ListMessagesAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ticket>> ListTicketsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    void AddTicket(Ticket ticket);

    Task<int> CountOpenConversationsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<int> CountOpenTicketsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
