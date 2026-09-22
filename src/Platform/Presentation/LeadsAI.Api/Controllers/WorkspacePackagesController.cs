using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Infrastructure.WorkspacePackages;

namespace LeadsAI.Api.Controllers;

public sealed record InstallWorkspacePackageRequest(string PackageId);
public sealed record BuildWorkspacePackageRequest(string Prompt);
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
public sealed class WorkspacePackagesController(ITenantContext tenant, WorkspacePackageInstaller installer) : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(new[]
    {
        WorkspacePackageCatalog.FusionFleetPromotion,
        WorkspacePackageCatalog.QualifyAiAcquisition,
        WorkspacePackageCatalog.Blank
    });

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
