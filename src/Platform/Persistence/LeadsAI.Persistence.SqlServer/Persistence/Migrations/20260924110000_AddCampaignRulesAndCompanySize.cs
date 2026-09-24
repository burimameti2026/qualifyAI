using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class AddCampaignRulesAndCompanySize : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('CampaignSteps', 'RulesJson') IS NULL
                ALTER TABLE [CampaignSteps]
                ADD [RulesJson] nvarchar(max) NOT NULL
                    CONSTRAINT [DF_CampaignSteps_RulesJson] DEFAULT N'{}';

            IF COL_LENGTH('Prospects', 'CompanySize') IS NULL
                ALTER TABLE [Prospects]
                ADD [CompanySize] int NOT NULL
                    CONSTRAINT [DF_Prospects_CompanySize] DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('CampaignSteps', 'RulesJson') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'[DF_CampaignSteps_RulesJson]', N'D') IS NOT NULL
                    ALTER TABLE [CampaignSteps] DROP CONSTRAINT [DF_CampaignSteps_RulesJson];
                ALTER TABLE [CampaignSteps] DROP COLUMN [RulesJson];
            END;

            IF COL_LENGTH('Prospects', 'CompanySize') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'[DF_Prospects_CompanySize]', N'D') IS NOT NULL
                    ALTER TABLE [Prospects] DROP CONSTRAINT [DF_Prospects_CompanySize];
                ALTER TABLE [Prospects] DROP COLUMN [CompanySize];
            END;
            """);
    }
}
