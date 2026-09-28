using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateAgentJobRuntime2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutonomousAcquisitionAgentRuns");

            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                table: "CampaignContainers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChangesJson",
                table: "CampaignContainers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "CampaignContainers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CampaignContainers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VersionLabel",
                table: "CampaignContainers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ContainerVersion",
                table: "AutonomousAcquisitionTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContainerVersion",
                table: "AgentJobs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskId",
                table: "AgentJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskPayloadJson",
                table: "AgentJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "AgentJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                table: "CampaignContainers");

            migrationBuilder.DropColumn(
                name: "ChangesJson",
                table: "CampaignContainers");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "CampaignContainers");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CampaignContainers");

            migrationBuilder.DropColumn(
                name: "VersionLabel",
                table: "CampaignContainers");

            migrationBuilder.DropColumn(
                name: "ContainerVersion",
                table: "AutonomousAcquisitionTasks");

            migrationBuilder.DropColumn(
                name: "ContainerVersion",
                table: "AgentJobs");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "AgentJobs");

            migrationBuilder.DropColumn(
                name: "TaskPayloadJson",
                table: "AgentJobs");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "AgentJobs");

            migrationBuilder.CreateTable(
                name: "AutonomousAcquisitionAgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiscoveredCount = table.Column<int>(type: "int", nullable: false),
                    EmailsQueuedCount = table.Column<int>(type: "int", nullable: false),
                    EmailsSentCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HighScoreCount = table.Column<int>(type: "int", nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousAcquisitionAgentRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgentRuns_AgentId_CampaignId_ScheduledAtUtc",
                table: "AutonomousAcquisitionAgentRuns",
                columns: new[] { "AgentId", "CampaignId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgentRuns_TenantId_Status",
                table: "AutonomousAcquisitionAgentRuns",
                columns: new[] { "TenantId", "Status" });
        }
    }
}
