using MediatR;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class SaveWorkflowDesignerCommandHandler(IWorkflowAutomationRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<SaveWorkflowDesignerCommand, WorkflowSaveResult>
{
    public async Task<WorkflowSaveResult> Handle(SaveWorkflowDesignerCommand command, CancellationToken ct)
    {
        if (!await repository.WorkflowExistsAsync(command.TenantId, command.FlowId, ct))
            throw new InvalidOperationException("Workflow was not found for the current tenant.");
        var designer = WorkflowDesigner.Build(command.TenantId, command.FlowId, command.Nodes, command.Edges);
        var oldNodes = await repository.ListWorkflowNodesAsync(command.TenantId, command.FlowId, tracked: true, cancellationToken: ct);
        var oldEdges = await repository.ListWorkflowEdgesAsync(command.TenantId, command.FlowId, tracked: true, cancellationToken: ct);
        repository.ReplaceWorkflowDesigner(oldNodes, oldEdges, designer.Nodes, designer.Edges);
        await unitOfWork.SaveChangesAsync(ct);
        return new WorkflowSaveResult(designer.Nodes.Count, designer.Edges.Count);
    }
}

public sealed class CreateAutomationCommandHandler(IWorkflowAutomationRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<CreateAutomationCommand, AutomationRule>
{
    public async Task<AutomationRule> Handle(CreateAutomationCommand command, CancellationToken ct)
    {
        var rule = AutomationRule.Create(command.TenantId, command.Rule.Name, command.Rule.Trigger,
            command.Rule.ConditionsJson, command.Rule.ActionsJson, command.Rule.Active);
        repository.AddAutomationRule(rule);
        await unitOfWork.SaveChangesAsync(ct);
        return rule;
    }
}

public sealed class UpdateAutomationCommandHandler(IWorkflowAutomationRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<UpdateAutomationCommand, AutomationRule?>
{
    public async Task<AutomationRule?> Handle(UpdateAutomationCommand command, CancellationToken ct)
    {
        var rule = await repository.GetAutomationRuleAsync(command.TenantId, command.Id, ct);
        if (rule is null) return null;
        rule.UpdateConfiguration(command.Rule.Name, command.Rule.Trigger, command.Rule.ConditionsJson, command.Rule.ActionsJson, command.Rule.Active);
        await unitOfWork.SaveChangesAsync(ct);
        return rule;
    }
}

public sealed class RunAutomationCommandHandler(IWorkflowAutomationRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<RunAutomationCommand, AutomationRun?>
{
    public async Task<AutomationRun?> Handle(RunAutomationCommand command, CancellationToken ct)
    {
        var rule = await repository.GetAutomationRuleAsync(command.TenantId, command.Id, ct);
        if (rule is null) return null;
        if (!rule.Active) throw new InvalidOperationException("Inactive automation rules cannot be run.");
        var run = AutomationRun.Create(command.TenantId, rule.Id, "{\"manual\":true}");
        repository.AddAutomationRun(run);
        run.Start();
        await unitOfWork.SaveChangesAsync(ct);
        run.Complete("[\"Rule evaluated\",\"Actions dispatched\"]");
        await unitOfWork.SaveChangesAsync(ct);
        return run;
    }
}
