using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Support;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inbox")]
public sealed class InboxController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("conversations")]
    public Task<IReadOnlyList<Conversation>> Conversations(CancellationToken ct) => sender.Send(new ListConversationsQuery(tenant.TenantId()), ct);

    [HttpGet("conversations/{id:guid}/messages")]
    public Task<IReadOnlyList<Message>> Messages(Guid id, CancellationToken ct) => sender.Send(new ListConversationMessagesQuery(tenant.TenantId(), id), ct);

    [HttpPost("conversations/{id:guid}/takeover")]
    public async Task<IActionResult> Takeover(Guid id, CancellationToken ct)
    {
        Guid? userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : null;
        return (await sender.Send(new TakeoverConversationCommand(tenant.TenantId(), id, userId), ct)) is { } x ? Ok(x) : NotFound();
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, MessageInput input, CancellationToken ct)
    {
        Guid? userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : null;
        return (await sender.Send(new AddConversationMessageCommand(tenant.TenantId(), id, userId, input.Text, input.SenderType), ct)) is { } x ? Ok(x) : NotFound();
    }

    [HttpPost("conversations/{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, NoteInput input, CancellationToken ct)
    {
        var userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : Guid.Empty;
        return Ok(await sender.Send(new AddConversationNoteCommand(tenant.TenantId(), id, userId, input.Text), ct));
    }

    [HttpPut("conversations/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ConversationUpdate input, CancellationToken ct)
        => (await sender.Send(new UpdateConversationCommand(tenant.TenantId(), id, input.Status, input.AiEnabled), ct)) is { } x ? Ok(x) : NotFound();
}

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<Ticket>> List(CancellationToken ct) => sender.Send(new ListTicketsQuery(tenant.TenantId()), ct);

    [HttpPost]
    public async Task<IActionResult> Create(Ticket input, CancellationToken ct)
    {
        var x = await sender.Send(new CreateTicketCommand(tenant.TenantId(), input.ConversationId, input.ContactId, input.Subject, input.Description, input.Priority, input.SlaPolicyId), ct);
        return Created($"/api/tickets/{x.Id}", x);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Ticket input, CancellationToken ct)
        => (await sender.Send(new UpdateTicketCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();
}

public sealed record MessageInput(string Text, string SenderType = "agent");
public sealed record NoteInput(string Text);
public sealed record ConversationUpdate(string Status, bool? AiEnabled = null);
