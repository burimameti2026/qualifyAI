using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Infrastructure.WorkspacePackages;

namespace LeadsAI.Infrastructure.Demo;

public sealed record ScenarioInstallResult(string Scenario, int Prospects, int Campaigns, int Opportunities, int Meetings, int Tickets, int Automations);
public sealed record ScenarioResetResult(int DeletedProspects, int DeletedLists, int DeletedAgents, int DeletedContacts, int DeletedLeads, int DeletedOpportunities, int DeletedPipelines, int DeletedMeetings, int DeletedAutomations);
public sealed record ResetAndInstallResult(ScenarioResetResult Reset, ScenarioInstallResult Scenario);

public sealed class RealisticScenarioService(WorkspacePackages.RealWorkspaceService workspace, AppDbContext db)
{
    public async Task<ScenarioInstallResult> InstallAsync(Guid tenantId, CancellationToken ct = default)
    {
        var draft = await workspace.PrepareAsync(
            tenantId,
            new PrepareRealWorkspaceRequest("logistics", "fusionfleet-promotion", "FusionFleet Promotion"),
            ct);

        var selected = draft.Prospects.Select(x => x.Id).ToArray();
        draft = await workspace.SaveAsync(
            tenantId,
            new SaveRealWorkspaceRequest(draft.WorkspaceId, draft.Name, draft.Prospects, selected),
            ct);

        await workspace.ActivateAsync(tenantId, draft.WorkspaceId, ct);
        return await SnapshotAsync(tenantId, "FusionFleet presentation workspace", ct);
    }

    public async Task<ScenarioResetResult> ResetBusinessDataAsync(Guid tenantId, CancellationToken ct = default)
    {
        var draft = await workspace.GetAsync(tenantId, ct);
        var deletedProspects = 0;

        if (draft is not null)
        {
            var source = $"real-workspace:{draft.WorkspaceId}";
            deletedProspects = await db.Prospects
                .Where(x => x.TenantId == tenantId && x.DatasetOrigin == source)
                .ExecuteDeleteAsync(ct);
        }

        var setting = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == "real-workspace.v1", ct);

        if (setting is not null)
        {
            db.TenantSettings.Remove(setting);
            await db.SaveChangesAsync(ct);
        }

        return new ScenarioResetResult(
            deletedProspects, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<ResetAndInstallResult> ResetAndInstallAsync(Guid tenantId, CancellationToken ct = default)
    {
        var reset = await ResetBusinessDataAsync(tenantId, ct);
        var scenario = await InstallAsync(tenantId, ct);
        return new ResetAndInstallResult(reset, scenario);
    }

    private async Task<ScenarioInstallResult> SnapshotAsync(Guid tenantId, string scenario, CancellationToken ct)
        => new(
            scenario,
            await db.Prospects.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Campaigns.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Opportunitys.CountAsync(x => x.TenantId == tenantId, ct),
            await db.MeetingBookings.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Tickets.CountAsync(x => x.TenantId == tenantId, ct),
            await db.AutomationRules.CountAsync(x => x.TenantId == tenantId, ct));
}
