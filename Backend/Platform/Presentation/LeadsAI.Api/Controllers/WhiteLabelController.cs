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
[RequireModule(QualifyAiModules.Settings)]
[Route("api/white-label")]
public sealed class WhiteLabelController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("branding")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<BrandingProfile?> Branding(CancellationToken ct) => sender.Send(new GetBrandingQuery(tenant.TenantId()), ct);

    [HttpPut("branding")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<BrandingProfile> UpdateBranding(BrandingProfile input, CancellationToken ct) => sender.Send(new UpdateBrandingCommand(tenant.TenantId(), input), ct);

    [HttpGet("domains")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<IReadOnlyList<CustomDomain>> Domains(CancellationToken ct) => sender.Send(new ListCustomDomainsQuery(tenant.TenantId()), ct);
}
