using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentJobExecutionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscoveredCount = table.Column<int>(type: "int", nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    HighScoreCount = table.Column<int>(type: "int", nullable: false),
                    EmailsQueuedCount = table.Column<int>(type: "int", nullable: false),
                    EmailsSentCount = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentJobs_LeaseUntilUtc",
                table: "AgentJobs",
                column: "LeaseUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgentJobs_TenantId_AgentId_Status",
                table: "AgentJobs",
                columns: new[] { "TenantId", "AgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentJobs_TenantId_CampaignId_ContainerId",
                table: "AgentJobs",
                columns: new[] { "TenantId", "CampaignId", "ContainerId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentJobs_TenantId_Status_ScheduledAtUtc_Priority",
                table: "AgentJobs",
                columns: new[] { "TenantId", "Status", "ScheduledAtUtc", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentJobs");
        }
    }
}
