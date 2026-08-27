using MediatR;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class SupportCommandHandlers(ISupportRepository support, IBusinessUnitOfWork unitOfWork) :
    IRequestHandler<TakeoverConversationCommand, Conversation?>,
    IRequestHandler<AddConversationMessageCommand, Message?>,
    IRequestHandler<AddConversationNoteCommand, ConversationNote>,
    IRequestHandler<UpdateConversationCommand, Conversation?>,
    IRequestHandler<UpdateTicketCommand, Ticket?>
{
    public async Task<Conversation?> Handle(TakeoverConversationCommand c, CancellationToken ct)
    {
        var x = await support.GetConversationAsync(c.TenantId, c.ConversationId, ct);
        if (x is null) return null;
        x.TakeOver(c.UserId);
        await unitOfWork.SaveChangesAsync(ct);
        return x;
    }

    public async Task<Message?> Handle(AddConversationMessageCommand c, CancellationToken ct)
    {
        var conversation = await support.GetConversationAsync(c.TenantId, c.ConversationId, ct);
        if (conversation is null) return null;
        var message = Message.Create(c.TenantId, c.ConversationId, c.UserId, c.Text, c.SenderType);
        support.AddMessage(message);
        conversation.RegisterMessage();
        await unitOfWork.SaveChangesAsync(ct);
        return message;
    }

    public async Task<ConversationNote> Handle(AddConversationNoteCommand c, CancellationToken ct)
    {
        if (await support.GetConversationAsync(c.TenantId, c.ConversationId, ct) is null)
            throw new InvalidOperationException("Conversation not found.");
        var note = ConversationNote.Create(c.TenantId, c.ConversationId, c.UserId, c.Text);
        support.AddNote(note);
        await unitOfWork.SaveChangesAsync(ct);
        return note;
    }

    public async Task<Conversation?> Handle(UpdateConversationCommand c, CancellationToken ct)
    {
        var x = await support.GetConversationAsync(c.TenantId, c.ConversationId, ct);
        if (x is null) return null;
        if (!Enum.TryParse<ConversationStatus>(c.Status, true, out var status))
            throw new InvalidOperationException("Invalid conversation status.");
        x.UpdateState(status, c.AiEnabled);
        await unitOfWork.SaveChangesAsync(ct);
        return x;
    }

    public async Task<Ticket?> Handle(UpdateTicketCommand c, CancellationToken ct)
    {
        var x = await support.GetTicketAsync(c.TenantId, c.Id, ct);
        if (x is null) return null;
        x.Update(c.Ticket.Subject, c.Ticket.Description, c.Ticket.Status, c.Ticket.Priority, c.Ticket.AssignedUserId, c.Ticket.SlaPolicyId);
        support.AddTicketEvent(TicketEvent.Updated(x));
        await unitOfWork.SaveChangesAsync(ct);
        return x;
    }
}
