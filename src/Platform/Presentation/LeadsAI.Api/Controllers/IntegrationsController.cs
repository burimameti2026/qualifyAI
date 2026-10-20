using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Infrastructure.Automation;
using LeadsAI.Persistence.SqlServer;
using MassTransit;
using LeadsAI.Automation.Application.IntegrationEvents;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Integrations)]
[Route("api/integrations")]
public sealed class IntegrationsController(ISender sender, ITenantContext tenant, IIntegrationRegistry registry) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.IntegrationsRead)]
    public Task<IReadOnlyList<IntegrationConnection>> List(CancellationToken ct) => sender.Send(new ListIntegrationsQuery(tenant.TenantId()), ct);

    [HttpGet("providers")]
    [RequirePermission(QualifyAiPermissions.IntegrationsRead)]
    public IActionResult Providers() => Ok(registry.Providers);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public Task<IntegrationConnection> Create(IntegrationConnection input, CancellationToken ct) => sender.Send(new CreateIntegrationCommand(tenant.TenantId(), input), ct);

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Update(Guid id, IntegrationConnection input, CancellationToken ct)
        => (await sender.Send(new UpdateIntegrationCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("{id:guid}/test")]
    [RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
        => (await sender.Send(new TestIntegrationCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();
}

public sealed record WorkflowDesignerInput(List<WorkflowNode> Nodes, List<WorkflowEdge> Edges);
public sealed record CreateWorkflowRequest(string Name, bool Active = true, string? Trigger = null);
public sealed record WorkflowOrchestrationRequest(Guid? CampaignId, Guid? PipelineId, IReadOnlyList<Guid> AutomationRuleIds, IReadOnlyList<Guid> ContainerIds, string? Trigger = null, bool Active = true);
