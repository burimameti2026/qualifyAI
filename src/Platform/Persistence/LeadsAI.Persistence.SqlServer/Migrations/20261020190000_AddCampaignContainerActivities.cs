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

            // Preserve previously recorded container activity before the API switches
            // to the dedicated operational history table.
            migrationBuilder.Sql(@"
INSERT INTO [CampaignContainerActivities]
    ([Id], [TenantId], [ContainerId], [RunId], [TaskId], [ContainerVersion], [AtUtc],
     [Level], [EventType], [StepType], [StepName], [Message], [DataJson])
SELECT
    NEWID(),
    a.[TenantId],
    TRY_CONVERT(uniqueidentifier, a.[EntityId]),
    TRY_CONVERT(uniqueidentifier, JSON_VALUE(a.[DataJson], '$.jobId')),
    TRY_CONVERT(uniqueidentifier, JSON_VALUE(a.[DataJson], '$.taskId')),
    0,
    a.[CreatedAtUtc],
    COALESCE(JSON_VALUE(a.[DataJson], '$.level'), 'info'),
    a.[Action],
    COALESCE(JSON_VALUE(a.[DataJson], '$.stepType'), ''),
    COALESCE(JSON_VALUE(a.[DataJson], '$.stepName'), ''),
    COALESCE(JSON_VALUE(a.[DataJson], '$.message'), a.[Action]),
    COALESCE(JSON_QUERY(a.[DataJson], '$.data'), a.[DataJson], '{}')
FROM [AuditLogs] a
WHERE a.[EntityType] = 'CampaignContainerActivity'
  AND TRY_CONVERT(uniqueidentifier, a.[EntityId]) IS NOT NULL
  AND ISJSON(a.[DataJson]) = 1;

DELETE FROM [AuditLogs]
WHERE [EntityType] = 'CampaignContainerActivity'
  AND TRY_CONVERT(uniqueidentifier, [EntityId]) IS NOT NULL
  AND ISJSON([DataJson]) = 1;
");
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
