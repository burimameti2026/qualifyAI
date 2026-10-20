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
[RequirePermission(QualifyAiPermissions.SettingsManage)]
[Route("api/security")]
public sealed class SecurityController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("sso")]
    public Task<IReadOnlyList<SsoConfiguration>> Sso(CancellationToken ct) => sender.Send(new ListSsoConfigurationsQuery(tenant.TenantId()), ct);

    [HttpGet("retention")]
    public Task<IReadOnlyList<DataRetentionPolicy>> Retention(CancellationToken ct) => sender.Send(new ListRetentionPoliciesQuery(tenant.TenantId()), ct);
}
