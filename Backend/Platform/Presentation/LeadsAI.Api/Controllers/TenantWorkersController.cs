using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(QualifyAiPermissions.SettingsManage)]
[Route("api/platform/workers")]
public sealed class TenantWorkersController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var settings = await db.TenantSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Key.StartsWith("worker.enabled."))
            .ToDictionaryAsync(x => x.Key, x => x.Value == "true", ct);

        return Ok(TenantWorkerRuntime.Definitions.Select(definition => new
        {
            definition.Key,
            definition.Name,
            definition.Description,
            Enabled = settings.TryGetValue(TenantWorkerRuntime.WorkerSettingKey(definition.Key), out var enabled) && enabled
        }));
    }

    [HttpPut("{workerKey}")]
    public async Task<IActionResult> Set(string workerKey, [FromBody] WorkerToggleRequest request, CancellationToken ct)
    {
        var definition = TenantWorkerRuntime.Definitions.FirstOrDefault(x => x.Key.Equals(workerKey, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
            return NotFound(new { code = "worker_not_found", workerKey });

        var tenantId = tenant.TenantId();
        var key = TenantWorkerRuntime.WorkerSettingKey(definition.Key);
        var setting = await db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == key, ct);

        if (setting is null)
            db.TenantSettings.Add(new TenantSetting { TenantId = tenantId, Key = key, Value = request.Enabled ? "true" : "false" });
        else
            setting.Value = request.Enabled ? "true" : "false";

        await db.SaveChangesAsync(ct);
        return Ok(new { definition.Key, definition.Name, definition.Description, Enabled = request.Enabled });
    }

    public sealed record WorkerToggleRequest(bool Enabled);
}
