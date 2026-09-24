using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260924110000_AddCampaignRulesAndCompanySize")]
public sealed class AddCampaignRulesAndCompanySize : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'CampaignSteps', N'RulesJson') IS NULL
            BEGIN
                ALTER TABLE [CampaignSteps]
                ADD [RulesJson] nvarchar(max) NOT NULL
                    CONSTRAINT [DF_CampaignSteps_RulesJson] DEFAULT N'{}';
            END
            """);

        migrationBuilder.Sql("""
            IF COL_LENGTH(N'Prospects', N'CompanySize') IS NULL
            BEGIN
                ALTER TABLE [Prospects]
                ADD [CompanySize] int NOT NULL
                    CONSTRAINT [DF_Prospects_CompanySize] DEFAULT 0;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'CampaignSteps', N'RulesJson') IS NOT NULL
                ALTER TABLE [CampaignSteps] DROP CONSTRAINT IF EXISTS [DF_CampaignSteps_RulesJson];
            IF COL_LENGTH(N'CampaignSteps', N'RulesJson') IS NOT NULL
                ALTER TABLE [CampaignSteps] DROP COLUMN [RulesJson];
            """);

        migrationBuilder.Sql("""
            IF COL_LENGTH(N'Prospects', N'CompanySize') IS NOT NULL
                ALTER TABLE [Prospects] DROP CONSTRAINT IF EXISTS [DF_Prospects_CompanySize];
            IF COL_LENGTH(N'Prospects', N'CompanySize') IS NOT NULL
                ALTER TABLE [Prospects] DROP COLUMN [CompanySize];
            """);
    }
}
