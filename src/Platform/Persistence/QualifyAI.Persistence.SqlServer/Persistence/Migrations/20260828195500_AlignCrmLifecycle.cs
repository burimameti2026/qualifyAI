using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations;

public partial class AlignCrmLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ClosedAtUtc",
            table: "Opportunitys",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LossReason",
            table: "Opportunitys",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AlterColumn<decimal>(
            name: "Probability",
            table: "PipelineStages",
            type: "decimal(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)");

        migrationBuilder.AlterColumn<string>(
            name: "Domain",
            table: "Companys",
            type: "nvarchar(253)",
            maxLength: 253,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Contacts",
            type: "nvarchar(320)",
            maxLength: 320,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.CreateIndex(
            name: "IX_Companys_TenantId_Domain",
            table: "Companys",
            columns: new[] { "TenantId", "Domain" });

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_TenantId_Email",
            table: "Contacts",
            columns: new[] { "TenantId", "Email" });

        migrationBuilder.CreateIndex(
            name: "IX_Opportunitys_TenantId_Status_PipelineStageId",
            table: "Opportunitys",
            columns: new[] { "TenantId", "Status", "PipelineStageId" });

        migrationBuilder.CreateIndex(
            name: "IX_Pipelines_TenantId_IsDefault",
            table: "Pipelines",
            columns: new[] { "TenantId", "IsDefault" });

        migrationBuilder.CreateIndex(
            name: "IX_PipelineStages_TenantId_PipelineId_SortOrder",
            table: "PipelineStages",
            columns: new[] { "TenantId", "PipelineId", "SortOrder" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Companys_TenantId_Domain", table: "Companys");
        migrationBuilder.DropIndex(name: "IX_Contacts_TenantId_Email", table: "Contacts");
        migrationBuilder.DropIndex(name: "IX_Opportunitys_TenantId_Status_PipelineStageId", table: "Opportunitys");
        migrationBuilder.DropIndex(name: "IX_Pipelines_TenantId_IsDefault", table: "Pipelines");
        migrationBuilder.DropIndex(name: "IX_PipelineStages_TenantId_PipelineId_SortOrder", table: "PipelineStages");

        migrationBuilder.DropColumn(name: "ClosedAtUtc", table: "Opportunitys");
        migrationBuilder.DropColumn(name: "LossReason", table: "Opportunitys");

        migrationBuilder.AlterColumn<string>(
            name: "Domain",
            table: "Companys",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(253)",
            oldMaxLength: 253);

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Contacts",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(320)",
            oldMaxLength: 320);

        migrationBuilder.AlterColumn<decimal>(
            name: "Probability",
            table: "PipelineStages",
            type: "decimal(18,2)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(5,2)",
            oldPrecision: 5,
            oldScale: 2);
    }
}
