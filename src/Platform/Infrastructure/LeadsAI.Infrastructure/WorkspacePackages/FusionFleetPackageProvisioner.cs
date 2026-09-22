using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed class FusionFleetPackageProvisioner(RealWorkspaceService workspace, AppDbContext db)
{
    public async Task<WorkspacePackageInstallResult> ProvisionAsync(Guid tenantId, CancellationToken ct = default)
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

        return new WorkspacePackageInstallResult(
            "fusionfleet-promotion",
            "FusionFleet Promotion",
            await db.Prospects.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Campaigns.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Opportunitys.CountAsync(x => x.TenantId == tenantId, ct),
            await db.MeetingBookings.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Tickets.CountAsync(x => x.TenantId == tenantId, ct),
            await db.AutomationRules.CountAsync(x => x.TenantId == tenantId, ct));
    }
}
