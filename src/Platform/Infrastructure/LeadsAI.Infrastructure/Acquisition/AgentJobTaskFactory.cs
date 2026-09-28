using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public interface IAgentJobTaskFactory
{
    Task<IReadOnlyList<AutonomousAcquisitionTask>> EnsureAsync(
        AgentJob job,
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        string campaignPlanJson,
        CancellationToken ct = default);
}

public sealed class AgentJobTaskFactory(
    AppDbContext db,
    IAutonomousAcquisitionWorkflowPlanner planner) : IAgentJobTaskFactory
{
    public async Task<IReadOnlyList<AutonomousAcquisitionTask>> EnsureAsync(
        AgentJob job,
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        string campaignPlanJson,
        CancellationToken ct = default)
    {
        var existing = await db.AutonomousAcquisitionTasks
            .Where(x => x.TenantId == job.TenantId &&
                        x.AgentId == agent.Id &&
                        x.RunId == job.Id)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        if (existing.Count > 0)
            return existing;

        var definitions = await planner.EnsurePlanAsync(agent, template, ct, campaignPlanJson);

        var tasks = definitions.Select(d => new AutonomousAcquisitionTask
        {
            TenantId = job.TenantId,
            AgentId = agent.Id,
            RunId = job.Id,
            ContainerVersion = job.ContainerVersion,
            Sequence = d.Sequence,
            Type = d.Type,
            Name = d.Name,
            Status = AutonomousAgentTaskStatus.Pending,
            RequiresApproval = d.RequiresApproval,
            ConfigurationJson = d.ConfigurationJson,
            ResultJson = "{}"
        }).ToList();

        db.AutonomousAcquisitionTasks.AddRange(tasks);
        await db.SaveChangesAsync(ct);
        return tasks;
    }
}
