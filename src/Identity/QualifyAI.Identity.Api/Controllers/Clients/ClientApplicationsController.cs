using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Clients.ManageClients;
using QualifyAI.Identity.Application.Clients.RegisterClient;

namespace QualifyAI.Identity.Api.Controllers.Clients;

[ApiController]
[Authorize]
[Route("api/identity/clients")]
public sealed class ClientApplicationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientApplicationResult>>> List(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new ListClientApplicationsQuery(tenantId), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RegisterClientApplicationResult>> Register(
        [FromBody] RegisterClientApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterClientApplicationCommand(
                request.TenantId,
                request.ClientId,
                request.DisplayName,
                request.Scopes),
            cancellationToken);

        return Created($"/api/identity/clients/{result.Id}", result);
    }

    [HttpPost("{id:guid}/rotate-secret")]
    public async Task<ActionResult<RotateClientSecretResult>> RotateSecret(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new RotateClientSecretCommand(id), cancellationToken));

    [HttpPost("{id:guid}/enable")]
    public Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
        => SetStatus(id, true, cancellationToken);

    [HttpPost("{id:guid}/disable")]
    public Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
        => SetStatus(id, false, cancellationToken);

    private async Task<IActionResult> SetStatus(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SetClientApplicationStatusCommand(id, enabled), cancellationToken);
        return NoContent();
    }
}

public sealed record RegisterClientApplicationRequest(
    Guid? TenantId,
    string ClientId,
    string DisplayName,
    IReadOnlyCollection<string> Scopes);
