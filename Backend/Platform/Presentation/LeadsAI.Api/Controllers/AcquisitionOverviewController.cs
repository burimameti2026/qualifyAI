using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Api.Services;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class AcquisitionOverviewController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("overview")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var id = TenantId;
        return Ok(new
        {
            discovered = await db.Prospects.CountAsync(x => x.TenantId==id, ct),
            hot = await db.Prospects.CountAsync(x => x.TenantId==id&&x.FitScore*55+x.IntentScore*45>=7500, ct),
            activeCampaigns = await db.Campaigns.CountAsync(x => x.TenantId==id&&x.Status==CampaignStatus.Running, ct),
            queuedMessages = await db.OutreachMessages.CountAsync(x => x.TenantId==id&&x.Status==OutreachStatus.Queued, ct),
            replies = await db.ProspectReplies.CountAsync(x => x.TenantId==id, ct),
            demoReady = await db.Prospects.CountAsync(x => x.TenantId==id&&x.Status==ProspectStatus.DemoReady, ct)
        });
    }
}
