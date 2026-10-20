using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Support;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LeadsAI.Api.Controllers;

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
