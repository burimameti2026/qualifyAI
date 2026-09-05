using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SyncPlatformModelAfterAutonomousAcquisitionAndProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Metric",
                table: "UsageRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Value",
                table: "UsageRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IcpProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "IcpProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CountriesCsv",
                table: "IcpProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "IcpProfiles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IntentKeywordsCsv",
                table: "IcpProfiles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDiscoveryAtUtc",
                table: "IcpProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumEmployees",
                table: "IcpProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumEmployees",
                table: "IcpProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutonomousAcquisitionAgentMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousAcquisitionAgentMemories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutonomousAcquisitionAgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscoveredCount = table.Column<int>(type: "int", nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    HighScoreCount = table.Column<int>(type: "int", nullable: false),
                    EmailsQueuedCount = table.Column<int>(type: "int", nullable: false),
                    EmailsSentCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousAcquisitionAgentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutonomousAcquisitionAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IcpJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinimumScore = table.Column<int>(type: "int", nullable: false),
                    DailyDiscoveryLimit = table.Column<int>(type: "int", nullable: false),
                    DailyEmailLimit = table.Column<int>(type: "int", nullable: false),
                    RunTimeUtc = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousAcquisitionAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignRecipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    DelayHours = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutreachMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutreachMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutreachMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentimentScore = table.Column<int>(type: "int", nullable: false),
                    RequiresHuman = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectReplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prospects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactReadiness = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SuggestedBuyer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeBand = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PainHypothesis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Offer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OutreachStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DatasetOrigin = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FitScore = table.Column<int>(type: "int", nullable: false),
                    IntentScore = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prospects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectSignals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TargetListMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetListMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TargetLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IcpProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dynamic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantBillingInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalInvoiceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantBillingLifecycles",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TrialEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GraceEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryAttempt = table.Column<int>(type: "int", nullable: false),
                    NextRetryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaymentState = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingLifecycles", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "TenantBillingSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalSubscriptionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPeriodEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLifecycleEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantModuleProvisionings",
                columns: table => new
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
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantModuleProvisionings", x => new { x.TenantId, x.ModuleCode });
                });

            migrationBuilder.CreateIndex(
                name: "IX_IcpProfiles_TenantId_Active",
                table: "IcpProfiles",
                columns: new[] { "TenantId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgentMemories_TenantId_AgentId_Category_Key",
                table: "AutonomousAcquisitionAgentMemories",
                columns: new[] { "TenantId", "AgentId", "Category", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgentRuns_AgentId_ScheduledAtUtc",
                table: "AutonomousAcquisitionAgentRuns",
                columns: new[] { "AgentId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgentRuns_TenantId_Status",
                table: "AutonomousAcquisitionAgentRuns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgents_TenantId_Status",
                table: "AutonomousAcquisitionAgents",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingEvents_Provider_ExternalEventId",
                table: "BillingEvents",
                columns: new[] { "Provider", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingEvents_TenantId_OccurredAtUtc",
                table: "BillingEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_Status_NextRunAtUtc",
                table: "CampaignRecipients",
                columns: new[] { "Status", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_TenantId_CampaignId_ProspectId",
                table: "CampaignRecipients",
                columns: new[] { "TenantId", "CampaignId", "ProspectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_TenantId_Status_StartsAtUtc",
                table: "Campaigns",
                columns: new[] { "TenantId", "Status", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSteps_TenantId_CampaignId_StepNumber",
                table: "CampaignSteps",
                columns: new[] { "TenantId", "CampaignId", "StepNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutreachMessages_TenantId_CampaignId_ProspectId",
                table: "OutreachMessages",
                columns: new[] { "TenantId", "CampaignId", "ProspectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_TenantId_CampaignId_ReceivedAtUtc",
                table: "ProspectReplies",
                columns: new[] { "TenantId", "CampaignId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_TenantId_Domain",
                table: "Prospects",
                columns: new[] { "TenantId", "Domain" },
                unique: true,
                filter: "[Domain] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_TenantId_Email",
                table: "Prospects",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_TenantId_Status_FitScore_IntentScore",
                table: "Prospects",
                columns: new[] { "TenantId", "Status", "FitScore", "IntentScore" });

            migrationBuilder.CreateIndex(
                name: "IX_ProspectSignals_TenantId_ProspectId_ObservedAtUtc",
                table: "ProspectSignals",
                columns: new[] { "TenantId", "ProspectId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TargetListMembers_TenantId_TargetListId_ProspectId",
                table: "TargetListMembers",
                columns: new[] { "TenantId", "TargetListId", "ProspectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetLists_TenantId_Name",
                table: "TargetLists",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingInvoices_Provider_ExternalInvoiceId",
                table: "TenantBillingInvoices",
                columns: new[] { "Provider", "ExternalInvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingInvoices_TenantId_UpdatedAtUtc",
                table: "TenantBillingInvoices",
                columns: new[] { "TenantId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingLifecycles_State_NextRetryAtUtc",
                table: "TenantBillingLifecycles",
                columns: new[] { "State", "NextRetryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingSubscriptions_Provider_ExternalSubscriptionId",
                table: "TenantBillingSubscriptions",
                columns: new[] { "Provider", "ExternalSubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingSubscriptions_TenantId",
                table: "TenantBillingSubscriptions",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantLifecycleEvents_CorrelationId",
                table: "TenantLifecycleEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantLifecycleEvents_TenantId_OccurredAtUtc",
                table: "TenantLifecycleEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantModuleProvisionings_Status_NextRetryAtUtc",
                table: "TenantModuleProvisionings",
                columns: new[] { "Status", "NextRetryAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutonomousAcquisitionAgentMemories");

            migrationBuilder.DropTable(
                name: "AutonomousAcquisitionAgentRuns");

            migrationBuilder.DropTable(
                name: "AutonomousAcquisitionAgents");

            migrationBuilder.DropTable(
                name: "BillingEvents");

            migrationBuilder.DropTable(
                name: "CampaignRecipients");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "CampaignSteps");

            migrationBuilder.DropTable(
                name: "OutreachMessages");

            migrationBuilder.DropTable(
                name: "ProspectReplies");

            migrationBuilder.DropTable(
                name: "Prospects");

            migrationBuilder.DropTable(
                name: "ProspectSignals");

            migrationBuilder.DropTable(
                name: "TargetListMembers");

            migrationBuilder.DropTable(
                name: "TargetLists");

            migrationBuilder.DropTable(
                name: "TenantBillingInvoices");

            migrationBuilder.DropTable(
                name: "TenantBillingLifecycles");

            migrationBuilder.DropTable(
                name: "TenantBillingSubscriptions");

            migrationBuilder.DropTable(
                name: "TenantLifecycleEvents");

            migrationBuilder.DropTable(
                name: "TenantModuleProvisionings");

            migrationBuilder.DropIndex(
                name: "IX_IcpProfiles_TenantId_Active",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "Metric",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "CountriesCsv",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "IntentKeywordsCsv",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "LastDiscoveryAtUtc",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "MaximumEmployees",
                table: "IcpProfiles");

            migrationBuilder.DropColumn(
                name: "MinimumEmployees",
                table: "IcpProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IcpProfiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
