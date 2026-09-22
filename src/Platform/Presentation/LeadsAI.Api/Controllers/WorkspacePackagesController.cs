using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Infrastructure.WorkspacePackages;

namespace LeadsAI.Api.Controllers;

public sealed record InstallWorkspacePackageRequest(string PackageId);
public sealed record BuildWorkspacePackageRequest(string Prompt);
public sealed record SaveWorkspacePackageRequest(string? Id, string Name, string Headline, string Subheadline, string Audience, string Price, string Billing, IReadOnlyList<string> Features, IReadOnlyList<string> Sections, IReadOnlyList<string> HiddenSections);
public sealed record SavedWorkspacePackage(Guid Id, string Name, string Headline, string Subheadline, string Audience, string Price, string Billing, IReadOnlyList<string> Features, IReadOnlyList<string> Sections, IReadOnlyList<string> HiddenSections, DateTime UpdatedAtUtc);
public sealed record WorkspacePackagePreview(
    string Name,
    string Headline,
    string Subheadline,
    string Audience,
    string Price,
    string Billing,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Sections,
    IReadOnlyList<string> SuggestedQuestions);

[ApiController]
[Authorize]
[Route("api/workspace-packages")]
public sealed class WorkspacePackagesController(ITenantContext tenant, WorkspacePackageInstaller installer, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var setting = await db.TenantSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant.TenantId() && x.Key == "acquisition.workspace-packages", ct);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return Ok(Array.Empty<SavedWorkspacePackage>());
        try
        {
            var packages = JsonSerializer.Deserialize<List<SavedWorkspacePackage>>(setting.Value) ?? new();
            return Ok(packages.OrderByDescending(x => x.UpdatedAtUtc));
        }
        catch (JsonException)
        {
            return Ok(Array.Empty<SavedWorkspacePackage>());
        }
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveWorkspacePackageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { detail = "Package name is required." });

        var tenantId = tenant.TenantId();
        var setting = await db.TenantSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Key == "acquisition.workspace-packages", ct);
        var packages = setting is null || string.IsNullOrWhiteSpace(setting.Value)
            ? new List<SavedWorkspacePackage>()
            : JsonSerializer.Deserialize<List<SavedWorkspacePackage>>(setting.Value) ?? new();

        var id = Guid.TryParse(request.Id, out var parsed) ? parsed : Guid.NewGuid();
        var saved = new SavedWorkspacePackage(id, request.Name.Trim(), request.Headline?.Trim() ?? "", request.Subheadline?.Trim() ?? "", request.Audience?.Trim() ?? "", request.Price?.Trim() ?? "", request.Billing?.Trim() ?? "month", request.Features ?? Array.Empty<string>(), request.Sections ?? Array.Empty<string>(), request.HiddenSections ?? Array.Empty<string>(), DateTime.UtcNow);

        var index = packages.FindIndex(x => x.Id == id);
        if (index >= 0) packages[index] = saved; else packages.Add(saved);

        var json = JsonSerializer.Serialize(packages);
        if (setting is null)
            db.TenantSettings.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "acquisition.workspace-packages", Value = json });
        else
            setting.Value = json;

        await db.SaveChangesAsync(ct);
        return Ok(saved);
    }

    [HttpPost("ai/build")]
    public IActionResult BuildWithAi([FromBody] BuildWorkspacePackageRequest request)
    {
        var prompt = request.Prompt?.Trim() ?? string.Empty;
        if (prompt.Length < 3)
            return BadRequest(new { detail = "Describe the product or offer you want to build." });

        var normalized = prompt.ToLowerInvariant();
        var fusion = normalized.Contains("fusionfleet") || normalized.Contains("fleet") ||
                     normalized.Contains("logistics") || normalized.Contains("transport");

        if (fusion)
        {
            return Ok(new WorkspacePackagePreview(
                "FusionFleet OPS",
                "Run your transport operation from one place.",
                "Orders, fleet, drivers, tracking and operational visibility in one platform.",
                "Logistics and transport companies · 10–500 employees",
                "299",
                "month",
                new[]
                {
                    "Transport orders",
                    "Live shipment tracking",
                    "Fleet management",
                    "Driver management",
                    "Operational analytics",
                    "Customer communication"
                },
                new[] { "Hero", "Features", "Business benefits", "How it works", "Pricing", "Call to action" },
                new[]
                {
                    "What is the starting price?",
                    "Which markets should we target first?",
                    "Should we position this for operations or fleet management?"
                }));
        }

        var name = string.Join(" ", prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4));
        return Ok(new WorkspacePackagePreview(
            name.Length > 0 ? name : "New Package",
            prompt,
            "A customer-ready offer generated from your business description.",
            "Define the target customer",
            "",
            "month",
            new[] { "Core capability", "Business outcome", "Operational visibility" },
            new[] { "Hero", "Features", "Business benefits", "How it works", "Pricing", "Call to action" },
            new[]
            {
                "Who is the ideal customer?",
                "What outcome does this offer create?",
                "What price and billing model should we use?"
            }));
    }

    [HttpPost("install")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Install([FromBody] InstallWorkspacePackageRequest request, CancellationToken ct)
        => await InstallCore(tenant.TenantId(), request, ct);

    [HttpPost("tenant/{tenantId:guid}/install")]
    [RequirePermission(QualifyAiPermissions.SystemAdmin)]
    public async Task<IActionResult> InstallForTenant(Guid tenantId, [FromBody] InstallWorkspacePackageRequest request, CancellationToken ct)
        => await InstallCore(tenantId, request, ct);

    private async Task<IActionResult> InstallCore(Guid tenantId, InstallWorkspacePackageRequest request, CancellationToken ct)
    {
        if (!WorkspacePackageCatalog.TryGet(request.PackageId, out var package))
            return BadRequest(new { detail = $"Unknown workspace package '{request.PackageId}'." });

        var result = await installer.InstallAsync(tenantId, package.Id, ct);
        return Ok(new
        {
            packageId = package.Id,
            package.Name,
            package.Version,
            tenantId,
            installed = true,
            alreadyInstalled = false,
            included = package.Included,
            result
        });
    }
}
