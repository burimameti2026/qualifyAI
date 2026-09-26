using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Application;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Billing)]
[RequirePermission(QualifyAiPermissions.BillingRead)]
[Route("api/billing")]
public sealed class BillingController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("plans")]
    public Task<IReadOnlyList<Plan>> Plans(CancellationToken ct) => sender.Send(new ListBillingPlansQuery(tenant.TenantId()), ct);

    [HttpGet("usage")]
    public Task<IReadOnlyList<UsageMeterDto>> Usage(CancellationToken ct) => sender.Send(new GetBillingUsageQuery(tenant.TenantId()), ct);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var plans = await sender.Send(new ListBillingPlansQuery(tenantId), ct);
        var usage = await sender.Send(new GetBillingUsageQuery(tenantId), ct);
        return Ok(new { plans, usage, generatedAtUtc = DateTime.UtcNow });
    }
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Settings)]
[RequirePermission(QualifyAiPermissions.SettingsManage)]
[Route("api/security")]
public sealed class SecurityController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("sso")]
    public Task<IReadOnlyList<SsoConfiguration>> Sso(CancellationToken ct) => sender.Send(new ListSsoConfigurationsQuery(tenant.TenantId()), ct);

    [HttpGet("retention")]
    public Task<IReadOnlyList<DataRetentionPolicy>> Retention(CancellationToken ct) => sender.Send(new ListRetentionPoliciesQuery(tenant.TenantId()), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Settings)]
[Route("api/white-label")]
public sealed class WhiteLabelController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("branding")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<BrandingProfile?> Branding(CancellationToken ct) => sender.Send(new GetBrandingQuery(tenant.TenantId()), ct);

    [HttpPut("branding")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<BrandingProfile> UpdateBranding(BrandingProfile input, CancellationToken ct) => sender.Send(new UpdateBrandingCommand(tenant.TenantId(), input), ct);

    [HttpGet("domains")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<IReadOnlyList<CustomDomain>> Domains(CancellationToken ct) => sender.Send(new ListCustomDomainsQuery(tenant.TenantId()), ct);
}




[ApiController]
[Authorize]
[RequirePermission(QualifyAiPermissions.AuditRead)]
[Route("api/platform")]
public sealed class PlatformController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("audit")]
    public Task<IReadOnlyList<AuditLog>> Audit(CancellationToken ct) => sender.Send(new ListAuditLogsQuery(tenant.TenantId()), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Analytics)]
[RequirePermission(QualifyAiPermissions.AnalyticsRead)]
[Route("api/revenue")]
public sealed class RevenueController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("attribution")]
    public Task<IReadOnlyList<RevenueAttribution>> Attribution(CancellationToken ct) => sender.Send(new ListRevenueAttributionQuery(tenant.TenantId()), ct);
}


[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/industry-packs")]
public sealed class IndustryPacksController(
    AppDbContext db,
    ITenantContext tenant,
    IIndustryPackProvisioner provisioner) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var packs = await db.IndustryPacks.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var tenantId = tenant.TenantId();
        var installed = await db.TenantIndustryPacks.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Enabled)
            .Select(x => x.IndustryPackId)
            .ToListAsync(ct);

        var provisioned = await db.Campaigns.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PackageCode != null && x.PackageCode != string.Empty)
            .Select(x => new { x.Id, x.PackageCode })
            .ToListAsync(ct);

        return Ok(packs.Select(pack =>
        {
            var marker = $"industry-pack:{pack.Code.Trim().ToLowerInvariant()}";
            var campaign = provisioned.FirstOrDefault(x => x.PackageCode == marker);
            return new
            {
                pack.Id,
                pack.Code,
                pack.Name,
                pack.Description,
                pack.TemplateJson,
                installed = installed.Contains(pack.Id),
                provisioned = campaign is not null,
                campaignId = campaign?.Id
            };
        }));
    }

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Create([FromBody] IndustryPack input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { detail = "Industry Pack code and name are required." });

        var code = input.Code.Trim().ToLowerInvariant();
        if (await db.IndustryPacks.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { detail = $"Industry Pack code '{code}' already exists." });

        var pack = new IndustryPack
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = input.Name.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            TemplateJson = string.IsNullOrWhiteSpace(input.TemplateJson) ? "{}" : input.TemplateJson
        };

        db.IndustryPacks.Add(pack);
        await db.SaveChangesAsync(ct);
        return Created($"/api/industry-packs/{pack.Id}", pack);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] IndustryPack input, CancellationToken ct)
    {
        var pack = await db.IndustryPacks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (pack is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { detail = "Industry Pack code and name are required." });

        var code = input.Code.Trim().ToLowerInvariant();
        if (await db.IndustryPacks.AnyAsync(x => x.Id != id && x.Code == code, ct))
            return Conflict(new { detail = $"Industry Pack code '{code}' already exists." });

        pack.Code = code;
        pack.Name = input.Name.Trim();
        pack.Description = input.Description?.Trim() ?? string.Empty;
        pack.TemplateJson = string.IsNullOrWhiteSpace(input.TemplateJson) ? "{}" : input.TemplateJson;
        pack.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(pack);
    }

    [HttpPost("ai/build")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> BuildWithAi([FromBody] IndustryPackAiBuildRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Prompt) || input.Prompt.Trim().Length < 3)
            return BadRequest(new { detail = "Describe the business, offer and customers you want this Industry Pack to target." });

        var system = "You are the LeadsAI Industry Pack Builder. Turn the user business description into a complete, usable acquisition pack. Return ONLY valid JSON with exactly these fields: code, name, description, industry, purpose, offer, audience, discoveryProvider, keywords, minimumScore, enrichmentEnabled, targetListEnabled, outreach, approvalRequired, scenarios. Fill every field with a concrete value. keywords and scenarios are comma-separated strings. discoveryProvider must be serpapi or manual; use serpapi for prospect discovery unless manual is explicitly requested. minimumScore is 0-100 and defaults to 70. Enrichment and target list should normally be true. approvalRequired should normally be true for outbound communication. code is lowercase kebab-case. outreach must be a concrete sequence/strategy. audience must describe target companies and buying signals. Do not invent a product price.";
        var raw = await provider.CompleteAsync(system, input.Prompt.Trim(), ct);
        try
        {
            var json = raw.Trim();
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                var first = json.IndexOf('\\n');
                var last = json.LastIndexOf("```", StringComparison.Ordinal);
                if (first >= 0 && last > first) json = json[(first + 1)..last].Trim();
            }
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return Ok(new IndustryPackAiBuildResult(
                GetString(root, "code"), GetString(root, "name"), GetString(root, "description"), GetString(root, "industry"),
                GetString(root, "purpose"), GetString(root, "offer"), GetString(root, "audience"), GetString(root, "discoveryProvider", "serpapi"),
                GetString(root, "keywords"), GetInt(root, "minimumScore", 70), GetBool(root, "enrichmentEnabled", true),
                GetBool(root, "targetListEnabled", true), GetString(root, "outreach"), GetBool(root, "approvalRequired", true), GetString(root, "scenarios")));
        }
        catch (JsonException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "The AI Builder returned an invalid pack definition. Please try again." });
        }

        static string GetString(JsonElement root, string name, string fallback = "") => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? fallback : fallback;
        static int GetInt(JsonElement root, string name, int fallback) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? Math.Clamp(number, 0, 100) : fallback;
        static bool GetBool(JsonElement root, string name, bool fallback) => root.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) ? value.GetBoolean() : fallback;
    }
    [HttpPost("{id:guid}/provision")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Provision(Guid id, [FromBody] IndustryPackProvisionRequest? input, CancellationToken ct)
    {
        try
        {
            var result = await provisioner.ProvisionAsync(
                tenant.TenantId(), id, ct, input?.ScenarioCode, input?.IcpProfileId);
            return Ok(new
            {
                result.IndustryPackId,
                result.IndustryCode,
                result.TargetListId,
                result.CampaignId,
                result.CampaignStatus,
                result.ProvisioningMode,
                result.Definition,
                result.AlreadyProvisioned
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/install")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public Task<IActionResult> Install(Guid id, CancellationToken ct)
        => Provision(id, null, ct);
}

public sealed record IndustryPackProvisionRequest(string? ScenarioCode, Guid? IcpProfileId);
public sealed record IndustryPackAiBuildRequest(string Prompt);
public sealed record IndustryPackAiBuildResult(string Code, string Name, string Description, string Industry, string Purpose, string Offer, string Audience, string DiscoveryProvider, string Keywords, int MinimumScore, bool EnrichmentEnabled, bool TargetListEnabled, string Outreach, bool ApprovalRequired, string Scenarios);
