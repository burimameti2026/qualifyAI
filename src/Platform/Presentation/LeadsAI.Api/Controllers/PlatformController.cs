using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Application;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(QualifyAiPermissions.AuditRead)]
[Route("api/platform")]
public sealed class PlatformController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("audit")]
    public Task<IReadOnlyList<AuditLog>> Audit(CancellationToken ct) => sender.Send(new ListAuditLogsQuery(tenant.TenantId()), ct);
}
