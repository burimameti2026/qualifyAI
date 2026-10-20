using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    public partial class AddCampaignContainerActivities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignContainerActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContainerVersion = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StepType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignContainerActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContainerActivities_TenantId_ContainerId_AtUtc",
                table: "CampaignContainerActivities",
                columns: new[] { "TenantId", "ContainerId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContainerActivities_TenantId_RunId_AtUtc",
                table: "CampaignContainerActivities",
                columns: new[] { "TenantId", "RunId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContainerActivities_TenantId_TaskId",
                table: "CampaignContainerActivities",
                columns: new[] { "TenantId", "TaskId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CampaignContainerActivities");
        }
    }
}
