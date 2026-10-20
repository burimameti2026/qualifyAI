using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260926230000_SeedAdditionalIndustryPacks")]
public partial class SeedAdditionalIndustryPacks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM [IndustryPacks] WHERE [Code] = N'b2b-saas')
BEGIN
    INSERT INTO [IndustryPacks] ([Id],[Code],[Name],[Description],[TemplateJson],[CreatedAtUtc],[UpdatedAtUtc])
    VALUES ('4b1d2a33-6f2a-4b77-9d3b-1c5e7a8f9012',N'b2b-saas',N'B2B SaaS',N'Prospecting and qualification for B2B software companies.',N'{"industry":"B2B Software","purpose":"Book qualified software demos","audience":"B2B companies with recurring software needs and identifiable technology buying signals","discovery":{"provider":"serpapi","keywords":["B2B software companies","SaaS companies","software buyers"]},"minimumScore":70,"enrichment":{"enabled":true},"targetList":{"enabled":true},"outreach":{"definition":"Personalized email sequence focused on the operational problem, relevant capability and a concise demo CTA."},"approvalRequired":true,"scenarios":["SaaS Companies","B2B Software Buyers"]}',SYSUTCDATETIME(),SYSUTCDATETIME());
END
IF NOT EXISTS (SELECT 1 FROM [IndustryPacks] WHERE [Code] = N'manufacturing')
BEGIN
    INSERT INTO [IndustryPacks] ([Id],[Code],[Name],[Description],[TemplateJson],[CreatedAtUtc],[UpdatedAtUtc])
    VALUES ('7c2e4b55-8a31-4d66-b2f4-9e6a1c3d5078',N'manufacturing',N'Manufacturing',N'Prospecting and qualification for manufacturers and industrial operators.',N'{"industry":"Manufacturing","purpose":"Find and qualify manufacturers with active operational needs","audience":"Manufacturing companies with production, logistics, procurement or capacity signals","discovery":{"provider":"serpapi","keywords":["manufacturing companies","industrial manufacturers","factories"]},"minimumScore":70,"enrichment":{"enabled":true},"targetList":{"enabled":true},"outreach":{"definition":"Research-led outreach tied to production, capacity, procurement or operational efficiency signals."},"approvalRequired":true,"scenarios":["Manufacturers","Industrial Operators"]}',SYSUTCDATETIME(),SYSUTCDATETIME());
END
IF NOT EXISTS (SELECT 1 FROM [IndustryPacks] WHERE [Code] = N'professional-services')
BEGIN
    INSERT INTO [IndustryPacks] ([Id],[Code],[Name],[Description],[TemplateJson],[CreatedAtUtc],[UpdatedAtUtc])
    VALUES ('9d3f6a77-2c45-4e88-a5b7-3f1d6c9024ab',N'professional-services',N'Professional Services',N'Prospecting and qualification for consulting, agencies and specialist service firms.',N'{"industry":"Professional Services","purpose":"Generate qualified conversations with service-led businesses","audience":"Consultancies, agencies and specialist firms showing growth, hiring or transformation signals","discovery":{"provider":"serpapi","keywords":["consulting firms","professional services companies","digital agencies"]},"minimumScore":70,"enrichment":{"enabled":true},"targetList":{"enabled":true},"outreach":{"definition":"Contextual outreach based on growth, hiring, transformation or service-delivery signals with a short conversation CTA."},"approvalRequired":true,"scenarios":["Consultancies","Agencies","Specialist Firms"]}',SYSUTCDATETIME(),SYSUTCDATETIME());
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DELETE FROM [IndustryPacks] WHERE [Code] IN (N'b2b-saas',N'manufacturing',N'professional-services');
""");
    }
}