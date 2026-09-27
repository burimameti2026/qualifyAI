using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Support;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Inbox)]
[Route("api/inbox")]
public sealed class InboxController(ISender sender, ITenantContext tenant, AppDbContext db) : ControllerBase
{
    [HttpGet("conversations")]
    [RequirePermission(QualifyAiPermissions.ConversationsRead)]
    public async Task<IActionResult> Conversations(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var rows = await (from conversation in db.Conversations.AsNoTracking()
                          join contact in db.Contacts.AsNoTracking() on conversation.ContactId equals (Guid?)contact.Id into contacts
                          from contact in contacts.DefaultIfEmpty()
                          join lead in db.Leads.AsNoTracking() on conversation.LeadId equals (Guid?)lead.Id into leads
                          from lead in leads.DefaultIfEmpty()
                          where conversation.TenantId == tenantId
                          orderby conversation.LastMessageAtUtc descending
                          select new
                          {
                              conversation.Id, conversation.ContactId, conversation.LeadId, conversation.ChannelId,
                              conversation.Status, conversation.AssignedUserId, conversation.AiEnabled, conversation.LastMessageAtUtc,
                              contactName = contact == null ? "" : (contact.FirstName + " " + contact.LastName).Trim(),
                              email = contact == null ? "" : contact.Email,
                              lifecycleStage = contact == null ? "" : contact.LifecycleStage,
                              leadScore = lead == null ? (int?)null : lead.Score,
                              leadStatus = lead == null ? "" : lead.Status,
                              intent = lead == null ? "" : lead.IntentSummary,
                              estimatedValue = lead == null ? null : lead.EstimatedValue,
                              lastMessage = db.Messages.Where(x => x.TenantId == tenantId && x.ConversationId == conversation.Id).OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Text).FirstOrDefault()
                          }).Take(200).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("conversations")]
    [RequirePermission(QualifyAiPermissions.ConversationsManage)]
    public async Task<IActionResult> CreateConversation(ConversationInput input, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        if (input.ContactId.HasValue && !await db.Contacts.AnyAsync(x => x.TenantId == tenantId && x.Id == input.ContactId, ct))
            return BadRequest(new { detail = "The selected contact does not belong to this tenant." });
        if (input.LeadId.HasValue && !await db.Leads.AnyAsync(x => x.TenantId == tenantId && x.Id == input.LeadId, ct))
            return BadRequest(new { detail = "The selected lead does not belong to this tenant." });
        if (!input.ContactId.HasValue && !input.LeadId.HasValue)
            return BadRequest(new { detail = "Select a contact or lead before opening a conversation." });
        var conversation = new Conversation { TenantId = tenantId, ContactId = input.ContactId, LeadId = input.LeadId, ChannelId = input.ChannelId, AiEnabled = input.AiEnabled, Status = ConversationStatus.Open };
        db.Conversations.Add(conversation);
        if (!string.IsNullOrWhiteSpace(input.InitialMessage))
        {
            var message = Message.Create(tenantId, conversation.Id, null, input.InitialMessage, "agent");
            db.Messages.Add(message);
            conversation.RegisterMessage(message.CreatedAtUtc);
        }
        db.AuditLogs.Add(new AuditLog { TenantId = tenantId, Action = "inbox.conversation.created", EntityType = nameof(Conversation), EntityId = conversation.Id.ToString(), DataJson = "{}" });
        await db.SaveChangesAsync(ct);
        return Created($"/api/inbox/conversations/{conversation.Id}", conversation);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    [RequirePermission(QualifyAiPermissions.ConversationsRead)]
    public Task<IReadOnlyList<Message>> Messages(Guid id, CancellationToken ct) => sender.Send(new ListConversationMessagesQuery(tenant.TenantId(), id), ct);

    [HttpPost("conversations/{id:guid}/takeover")]
    [RequirePermission(QualifyAiPermissions.ConversationsManage)]
    public async Task<IActionResult> Takeover(Guid id, CancellationToken ct)
    {
        Guid? userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : null;
        return (await sender.Send(new TakeoverConversationCommand(tenant.TenantId(), id, userId), ct)) is { } x ? Ok(x) : NotFound();
    }

    [HttpPost("conversations/{id:guid}/messages")]
    [RequirePermission(QualifyAiPermissions.ConversationsManage)]
    public async Task<IActionResult> AddMessage(Guid id, MessageInput input, CancellationToken ct)
    {
        Guid? userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : null;
        return (await sender.Send(new AddConversationMessageCommand(tenant.TenantId(), id, userId, input.Text, input.SenderType), ct)) is { } x ? Ok(x) : NotFound();
    }

    [HttpPost("conversations/{id:guid}/notes")]
    [RequirePermission(QualifyAiPermissions.ConversationsManage)]
    public async Task<IActionResult> AddNote(Guid id, NoteInput input, CancellationToken ct)
    {
        var userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : Guid.Empty;
        return Ok(await sender.Send(new AddConversationNoteCommand(tenant.TenantId(), id, userId, input.Text), ct));
    }

    [HttpPut("conversations/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.ConversationsManage)]
    public async Task<IActionResult> Update(Guid id, ConversationUpdate input, CancellationToken ct)
        => (await sender.Send(new UpdateConversationCommand(tenant.TenantId(), id, input.Status, input.AiEnabled), ct)) is { } x ? Ok(x) : NotFound();
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Ticketing)]
[Route("api/tickets")]
public sealed class TicketsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.TicketsRead)]
    public Task<IReadOnlyList<Ticket>> List(CancellationToken ct) => sender.Send(new ListTicketsQuery(tenant.TenantId()), ct);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.TicketsManage)]
    public async Task<IActionResult> Create(Ticket input, CancellationToken ct)
    {
        var x = await sender.Send(new CreateTicketCommand(tenant.TenantId(), input.ConversationId, input.ContactId, input.Subject, input.Description, input.Priority, input.SlaPolicyId), ct);
        return Created($"/api/tickets/{x.Id}", x);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.TicketsManage)]
    public async Task<IActionResult> Update(Guid id, Ticket input, CancellationToken ct)
        => (await sender.Send(new UpdateTicketCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();
}

public sealed record MessageInput(string Text, string SenderType = "agent");
public sealed record NoteInput(string Text);
public sealed record ConversationUpdate(string Status, bool? AiEnabled = null);
public sealed record ConversationInput(Guid? ContactId, Guid? LeadId, Guid? ChannelId, bool AiEnabled = true, string? InitialMessage = null);
