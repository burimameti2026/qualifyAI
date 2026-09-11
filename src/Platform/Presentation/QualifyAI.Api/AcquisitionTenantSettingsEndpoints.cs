using Microsoft.EntityFrameworkCore;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class AcquisitionTenantSettingsEndpoints
{
    private const string SerpApiKey = "acquisition.serpapi.apiKey";
    private const string MonthlyLimit = "acquisition.serpapi.monthlySafetyLimit";
    private const string TimeZone = "acquisition.schedule.timeZoneId";

    public static IEndpointRouteBuilder MapAcquisitionTenantSettings(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/autonomous-acquisition/settings");

        g.MapGet("", async (AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId();
            var settings = await db.TenantSettings.Where(x => x.TenantId == tenantId &&
                (x.Key == SerpApiKey || x.Key == MonthlyLimit || x.Key == TimeZone)).ToListAsync(ct);
            string Get(string key, string fallback = "") => settings.FirstOrDefault(x => x.Key == key)?.Value ?? fallback;
            return Results.Ok(new
            {
                hasSerpApiKey = !string.IsNullOrWhiteSpace(Get(SerpApiKey)),
                monthlySafetyLimit = int.TryParse(Get(MonthlyLimit, "200"), out var limit) ? Math.Clamp(limit, 1, 100000) : 200,
                timeZoneId = Get(TimeZone, "UTC")
            });
        });

        g.MapPut("", async (AcquisitionTenantSettingsInput input, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId();
            var settings = await db.TenantSettings.Where(x => x.TenantId == tenantId &&
                (x.Key == SerpApiKey || x.Key == MonthlyLimit || x.Key == TimeZone)).ToListAsync(ct);

            async Task Upsert(string key, string value, bool preserveWhenBlank = false)
            {
                var setting = settings.FirstOrDefault(x => x.Key == key);
                if (setting is null)
                {
                    if (preserveWhenBlank && string.IsNullOrWhiteSpace(value)) return;
                    db.TenantSettings.Add(new QualifyAI.Domain.TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = key, Value = value });
                }
                else if (!preserveWhenBlank || !string.IsNullOrWhiteSpace(value)) setting.Value = value;
                await Task.CompletedTask;
            }

            await Upsert(SerpApiKey, input.SerpApiApiKey ?? "", true);
            await Upsert(MonthlyLimit, Math.Clamp(input.MonthlySafetyLimit, 1, 100000).ToString());
            await Upsert(TimeZone, string.IsNullOrWhiteSpace(input.TimeZoneId) ? "UTC" : input.TimeZoneId.Trim());
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                hasSerpApiKey = await db.TenantSettings.AnyAsync(x => x.TenantId == tenantId && x.Key == SerpApiKey && x.Value != "", ct),
                monthlySafetyLimit = Math.Clamp(input.MonthlySafetyLimit, 1, 100000),
                timeZoneId = string.IsNullOrWhiteSpace(input.TimeZoneId) ? "UTC" : input.TimeZoneId.Trim()
            });
        });

        return app;
    }

    public sealed record AcquisitionTenantSettingsInput(string? SerpApiApiKey, int MonthlySafetyLimit = 200, string TimeZoneId = "UTC");
}
