using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Automation)]
[Route("api/workflows")]
public sealed class WorkflowsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<IReadOnlyList<QualificationFlow>> List(CancellationToken ct) => sender.Send(new ListWorkflowsQuery(tenant.TenantId()), ct);

    [HttpGet("{id:guid}/designer")]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<WorkflowDesignerDto> Designer(Guid id, CancellationToken ct) => sender.Send(new GetWorkflowDesignerQuery(tenant.TenantId(), id), ct);

    [HttpPut("{id:guid}/designer")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<WorkflowSaveResult> Save(Guid id, WorkflowDesignerInput input, CancellationToken ct)
        => sender.Send(new SaveWorkflowDesignerCommand(tenant.TenantId(), id, input.Nodes, input.Edges), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Automation)]
[Route("api/automations")]
public sealed class AutomationsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<IReadOnlyList<AutomationRule>> List(CancellationToken ct) => sender.Send(new ListAutomationsQuery(tenant.TenantId()), ct);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<AutomationRule> Create(AutomationRule input, CancellationToken ct) => sender.Send(new CreateAutomationCommand(tenant.TenantId(), input), ct);

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Update(Guid id, AutomationRule input, CancellationToken ct)
        => (await sender.Send(new UpdateAutomationCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("{id:guid}/run")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
        => (await sender.Send(new RunAutomationCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();
}

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

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/meetings")]
public sealed class MeetingsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<MeetingBooking>> List(CancellationToken ct) => sender.Send(new ListMeetingsQuery(tenant.TenantId()), ct);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Create(MeetingBooking input, CancellationToken ct)
    {
        var x = await sender.Send(new CreateMeetingCommand(tenant.TenantId(), input), ct);
        return Created($"/api/meetings/{x.Id}", x);
    }
}

public sealed record WorkflowDesignerInput(List<WorkflowNode> Nodes, List<WorkflowEdge> Edges);
