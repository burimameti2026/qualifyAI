using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260926205000_AddWorkflowOrchestrationBindings")]
public partial class AddWorkflowOrchestrationBindings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CampaignId",
            table: "QualificationFlows",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PipelineId",
            table: "QualificationFlows",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AutomationRuleIdsJson",
            table: "QualificationFlows",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "ContainerIdsJson",
            table: "QualificationFlows",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "Trigger",
            table: "QualificationFlows",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "manual");

        migrationBuilder.CreateIndex(
            name: "IX_QualificationFlows_TenantId_CampaignId",
            table: "QualificationFlows",
            columns: new[] { "TenantId", "CampaignId" });

        migrationBuilder.CreateIndex(
            name: "IX_QualificationFlows_TenantId_PipelineId",
            table: "QualificationFlows",
            columns: new[] { "TenantId", "PipelineId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_QualificationFlows_TenantId_CampaignId",
            table: "QualificationFlows");

        migrationBuilder.DropIndex(
            name: "IX_QualificationFlows_TenantId_PipelineId",
            table: "QualificationFlows");

        migrationBuilder.DropColumn(name: "CampaignId", table: "QualificationFlows");
        migrationBuilder.DropColumn(name: "PipelineId", table: "QualificationFlows");
        migrationBuilder.DropColumn(name: "AutomationRuleIdsJson", table: "QualificationFlows");
        migrationBuilder.DropColumn(name: "ContainerIdsJson", table: "QualificationFlows");
        migrationBuilder.DropColumn(name: "Trigger", table: "QualificationFlows");
    }
}
