using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Application;
using LeadsAI.Domain;

namespace LeadsAI.Infrastructure;

public sealed class CreateIcpTool(AppDbContext db) : IAiTool
{
    public string Name => "CreateIcp";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var root = doc.RootElement;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) return new(false, "{}", "name is required.");

        var profile = new IcpProfile
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Name = name.Trim(),
            Industry = Get(root, "industry"),
            CountriesCsv = Get(root, "countriesCsv"),
            IntentKeywordsCsv = Get(root, "intentKeywordsCsv"),
            MinimumEmployees = GetNullableInt(root, "minimumEmployees"),
            MaximumEmployees = GetNullableInt(root, "maximumEmployees"),
            CriteriaJson = JsonSerializer.Serialize(new { minimumScore = GetInt(root, "minimumScore", 70) }),
            Active = true,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.IcpProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return new(true, JsonSerializer.Serialize(new { profile.Id, profile.Name, profile.Industry, profile.CountriesCsv, profile.MinimumEmployees, profile.MaximumEmployees, profile.IntentKeywordsCsv }));
    }

    private static string Get(JsonElement r, string p) => r.TryGetProperty(p, out var v) ? v.GetString() ?? "" : "";
    private static int GetInt(JsonElement r, string p, int fallback) => r.TryGetProperty(p, out var v) && v.TryGetInt32(out var i) ? Math.Clamp(i, 0, 100) : fallback;
    private static int? GetNullableInt(JsonElement r, string p) => r.TryGetProperty(p, out var v) && v.TryGetInt32(out var i) ? i : null;
}

public sealed class GetIcpTool(AppDbContext db) : IAiTool
{
    public string Name => "GetIcp";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        var rows = await db.IcpProfiles.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Industry, x.CountriesCsv, x.MinimumEmployees, x.MaximumEmployees, x.IntentKeywordsCsv, x.CriteriaJson, x.Active }).ToListAsync(ct);
        return new(true, JsonSerializer.Serialize(rows));
    }
}

public sealed class GetCampaignTool(AppDbContext db) : IAiTool
{
    public string Name => "GetCampaign";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        if (!doc.RootElement.TryGetProperty("campaignId", out var p) || !Guid.TryParse(p.GetString(), out var id))
            return new(false, "{}", "campaignId is required.");
        var campaign = await db.Campaigns.AsNoTracking().Where(x => x.TenantId == context.TenantId && x.Id == id)
            .Select(x => new { x.Id, x.Name, x.Goal, x.Objective, x.Status, x.TargetListId, x.PackageCode, x.PlanStatus, x.PlanJson }).SingleOrDefaultAsync(ct);
        return campaign is null ? new(false, "{}", "campaign not found.") : new(true, JsonSerializer.Serialize(campaign));
    }
}