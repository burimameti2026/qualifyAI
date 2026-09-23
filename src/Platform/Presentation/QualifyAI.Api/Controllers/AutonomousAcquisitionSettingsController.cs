using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/autonomous-acquisition/settings")]
public sealed class AutonomousAcquisitionSettingsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private const string ApiKey = "ProspectDiscovery:SerpApi:ApiKey";
    private const string MonthlyLimit = "ProspectDiscovery:SerpApi:MonthlySafetyLimit";
    private const string TimeZone = "AutonomousAcquisition:TimeZoneId";
    private Guid TenantId => tenant.TenantId();

    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var rows = await db.TenantSettings.AsNoTracking().Where(x => x.TenantId == TenantId && (x.Key == ApiKey || x.Key == MonthlyLimit || x.Key == TimeZone)).ToListAsync(ct);
        var key = rows.FirstOrDefault(x => x.Key == ApiKey)?.Value;
        var limit = rows.FirstOrDefault(x => x.Key == MonthlyLimit)?.Value;
        var timeZone = rows.FirstOrDefault(x => x.Key == TimeZone)?.Value;
        return Ok(new { hasSerpApiKey = !string.IsNullOrWhiteSpace(key), monthlySafetyLimit = int.TryParse(limit, out var parsed) && parsed > 0 ? parsed : 200, timeZoneId = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone });
    }

    [HttpPut]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Save(AutonomousAcquisitionSettingsInput input, CancellationToken ct)
    {
        var limit = Math.Clamp(input.MonthlySafetyLimit <= 0 ? 200 : input.MonthlySafetyLimit, 1, 100000);
        if(!string.IsNullOrWhiteSpace(input.SerpApiApiKey)) await UpsertAsync(ApiKey, input.SerpApiApiKey.Trim(), ct);
        await UpsertAsync(MonthlyLimit, limit.ToString(), ct);
        await UpsertAsync(TimeZone, string.IsNullOrWhiteSpace(input.TimeZoneId) ? "UTC" : input.TimeZoneId.Trim(), ct);
        await db.SaveChangesAsync(ct);
        var hasKey = await db.TenantSettings.AnyAsync(x => x.TenantId == TenantId && x.Key == ApiKey && !string.IsNullOrWhiteSpace(x.Value), ct);
        return Ok(new { hasSerpApiKey = hasKey, monthlySafetyLimit = limit, timeZoneId = string.IsNullOrWhiteSpace(input.TimeZoneId) ? "UTC" : input.TimeZoneId.Trim() });
    }

    private async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var row = await db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Key == key, ct);
        if(row is null) db.TenantSettings.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = TenantId, Key = key, Value = value, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        else { row.Value = value; row.UpdatedAtUtc = DateTime.UtcNow; }
    }
}

public sealed record AutonomousAcquisitionSettingsInput(string? SerpApiApiKey, int MonthlySafetyLimit = 200, string? TimeZoneId = "UTC");
