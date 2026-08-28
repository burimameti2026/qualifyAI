using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Repositories;

public sealed class SupportRepository(AppDbContext dbContext) : ISupportRepository
{
    public async Task<IReadOnlyList<Conversation>> ListConversationsAsync(Guid tenantId, int take = 300, CancellationToken cancellationToken = default)
        => await dbContext.Conversations.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.LastMessageAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Message>> ListMessagesAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default)
        => await dbContext.Messages.AsNoTracking().Where(x => x.TenantId == tenantId && x.ConversationId == conversationId).OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<Conversation?> GetConversationAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default)
        => dbContext.Conversations.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == conversationId, cancellationToken);

    public void AddMessage(Message message) => dbContext.Messages.Add(message);
    public void AddNote(ConversationNote note) => dbContext.ConversationNotes.Add(note);

    public async Task<IReadOnlyList<Ticket>> ListTicketsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await dbContext.Tickets.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<Ticket?> GetTicketAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default)
        => dbContext.Tickets.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == ticketId, cancellationToken);

    public void AddTicket(Ticket ticket) => dbContext.Tickets.Add(ticket);
    public void AddTicketEvent(TicketEvent ticketEvent) => dbContext.TicketEvents.Add(ticketEvent);

    public Task<int> CountOpenConversationsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Conversations.CountAsync(x => x.TenantId == tenantId && x.Status == ConversationStatus.Open, cancellationToken);

    public Task<int> CountOpenTicketsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Tickets.CountAsync(x => x.TenantId == tenantId && x.Status != TicketStatus.Closed && x.Status != TicketStatus.Resolved, cancellationToken);
}
