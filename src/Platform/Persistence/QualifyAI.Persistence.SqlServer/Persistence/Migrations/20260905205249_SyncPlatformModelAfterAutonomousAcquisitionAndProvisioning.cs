using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations
{
    public partial class SyncPlatformModelAfterAutonomousAcquisitionAndProvisioning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Metric", table: "UsageRecords", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<long>(name: "Value", table: "UsageRecords", type: "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<string>(name: "Message", table: "Notifications", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Type", table: "Notifications", type: "nvarchar(max)", nullable: false, defaultValue: "");

            migrationBuilder.CreateTable(name: "AutonomousAcquisitionAgentMemories", columns: table => new { Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false), TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false), Value = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false), Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false), CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false) }, constraints: table => table.PrimaryKey("PK_AutonomousAcquisitionAgentMemories", x => x.Id));

            migrationBuilder.CreateTable(name: "AutonomousAcquisitionAgentRuns", columns: table => new { Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false), TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), Status = table.Column<int>(type: "int", nullable: false), IsManual = table.Column<bool>(type: "bit", nullable: false), Query = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true), ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false), StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true), CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true), DiscoveredCount = table.Column<int>(type: "int", nullable: false), QualifiedCount = table.Column<int>(type: "int", nullable: false), HighScoreCount = table.Column<int>(type: "int", nullable: false), EmailsQueuedCount = table.Column<int>(type: "int", nullable: false), EmailsSentCount = table.Column<int>(type: "int", nullable: false), Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true) }, constraints: table => table.PrimaryKey("PK_AutonomousAcquisitionAgentRuns", x => x.Id));

            migrationBuilder.CreateTable(name: "AutonomousAcquisitionAgents", columns: table => new { Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false), TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false), TemplateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false), Industry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false), Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false), CountriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false), IcpJson = table.Column<string>(type: "nvarchar(max)", nullable: false), MinimumScore = table.Column<int>(type: "int", nullable: false), DailyDiscoveryLimit = table.Column<int>(type: "int", nullable: false), DailyEmailLimit = table.Column<int>(type: "int", nullable: false), RunTimeUtc = table.Column<TimeOnly>(type: "time", nullable: false), Status = table.Column<int>(type: "int", nullable: false), LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true), CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false) }, constraints: table => table.PrimaryKey("PK_AutonomousAcquisitionAgents", x => x.Id));

            // BillingEvents and related billing projection tables are owned by EnsureBillingSchemaAsync().

            migrationBuilder.CreateTable(name: "TenantModuleProvisionings", columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModuleCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                NextRetryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_TenantModuleProvisionings", x => new { x.TenantId, x.ModuleCode }));

            migrationBuilder.CreateIndex("IX_AutonomousAcquisitionAgents_TenantId_Status", "AutonomousAcquisitionAgents", new[] { "TenantId", "Status" });
            migrationBuilder.CreateIndex("IX_AutonomousAcquisitionAgentRuns_TenantId_AgentId_ScheduledAtUtc", "AutonomousAcquisitionAgentRuns", new[] { "TenantId", "AgentId", "ScheduledAtUtc" });
            migrationBuilder.CreateIndex("IX_AutonomousAcquisitionAgentMemories_TenantId_AgentId_Key", "AutonomousAcquisitionAgentMemories", new[] { "TenantId", "AgentId", "Key" }, unique: true);
            migrationBuilder.CreateIndex("IX_TenantModuleProvisionings_Status_NextRetryAtUtc", "TenantModuleProvisionings", new[] { "Status", "NextRetryAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TenantModuleProvisionings");
            migrationBuilder.DropTable(name: "AutonomousAcquisitionAgents");
            migrationBuilder.DropTable(name: "AutonomousAcquisitionAgentRuns");
            migrationBuilder.DropTable(name: "AutonomousAcquisitionAgentMemories");
            migrationBuilder.DropColumn(name: "Metric", table: "UsageRecords");
            migrationBuilder.DropColumn(name: "Value", table: "UsageRecords");
            migrationBuilder.DropColumn(name: "Message", table: "Notifications");
            migrationBuilder.DropColumn(name: "Type", table: "Notifications");
        }
    }
}
