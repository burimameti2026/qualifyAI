using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Tenants.CreateTenant;
using QualifyAI.Identity.Application.Tenants.GetTenant;

namespace QualifyAI.Identity.Api.Controllers.Tenants;

[ApiController]
[Authorize]
[Route("api/identity/tenants")]
public sealed class TenantsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateTenantResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateTenantResult>> Create(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateTenantCommand(request.Name, request.Slug, request.ContactEmail),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { tenantId = result.Id }, result);
    }

    [HttpGet("{tenantId:guid}")]
    [ProducesResponseType<TenantDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDetails>> GetById(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await sender.Send(new GetTenantQuery(tenantId), cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}

public sealed record CreateTenantRequest(
    string Name,
    string Slug,
    string ContactEmail);
