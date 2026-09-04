using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260829090000_AddAcquisitionCampaignEngine")]
public sealed class AddAcquisitionCampaignEngine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("Active", "IcpProfiles", "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<string>("CountriesCsv", "IcpProfiles", "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("Industry", "IcpProfiles", "nvarchar(120)", maxLength: 120, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("IntentKeywordsCsv", "IcpProfiles", "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<DateTime>("LastDiscoveryAtUtc", "IcpProfiles", "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>("MaximumEmployees", "IcpProfiles", "int", nullable: true);
        migrationBuilder.AddColumn<int>("MinimumEmployees", "IcpProfiles", "int", nullable: true);
        migrationBuilder.AlterColumn<string>("Name", "IcpProfiles", "nvarchar(200)", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
        migrationBuilder.CreateIndex("IX_IcpProfiles_TenantId_Active", "IcpProfiles", new[] { "TenantId", "Active" });

        migrationBuilder.CreateTable("Campaigns", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            TargetListId = table.Column<Guid>("uniqueidentifier", nullable: false), Name = table.Column<string>("nvarchar(max)", nullable: false),
            Goal = table.Column<string>("nvarchar(max)", nullable: false), Status = table.Column<int>("int", nullable: false),
            SenderName = table.Column<string>("nvarchar(max)", nullable: false), SenderEmail = table.Column<string>("nvarchar(max)", nullable: false),
            StartsAtUtc = table.Column<DateTime>("datetime2", nullable: true), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Campaigns", x => x.Id));
        migrationBuilder.CreateIndex("IX_Campaigns_TenantId_Status_StartsAtUtc", "Campaigns", new[] { "TenantId", "Status", "StartsAtUtc" });

        migrationBuilder.CreateTable("CampaignRecipients", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), CampaignId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ProspectId = table.Column<Guid>("uniqueidentifier", nullable: false), CurrentStep = table.Column<int>("int", nullable: false), Status = table.Column<string>("nvarchar(40)", maxLength: 40, nullable: false),
            NextRunAtUtc = table.Column<DateTime>("datetime2", nullable: true), RepliedAtUtc = table.Column<DateTime>("datetime2", nullable: true), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_CampaignRecipients", x => x.Id));
        migrationBuilder.CreateIndex("IX_CampaignRecipients_Status_NextRunAtUtc", "CampaignRecipients", new[] { "Status", "NextRunAtUtc" });
        migrationBuilder.CreateIndex("IX_CampaignRecipients_TenantId_CampaignId_ProspectId", "CampaignRecipients", new[] { "TenantId", "CampaignId", "ProspectId" }, unique: true);

        migrationBuilder.CreateTable("CampaignSteps", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), CampaignId = table.Column<Guid>("uniqueidentifier", nullable: false),
            StepNumber = table.Column<int>("int", nullable: false), DelayHours = table.Column<int>("int", nullable: false), Channel = table.Column<string>("nvarchar(max)", nullable: false),
            SubjectTemplate = table.Column<string>("nvarchar(max)", nullable: false), BodyTemplate = table.Column<string>("nvarchar(max)", nullable: false), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_CampaignSteps", x => x.Id));
        migrationBuilder.CreateIndex("IX_CampaignSteps_TenantId_CampaignId_StepNumber", "CampaignSteps", new[] { "TenantId", "CampaignId", "StepNumber" }, unique: true);

        migrationBuilder.CreateTable("Prospects", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), CompanyId = table.Column<Guid>("uniqueidentifier", nullable: true), ContactId = table.Column<Guid>("uniqueidentifier", nullable: true),
            CompanyName = table.Column<string>("nvarchar(250)", maxLength: 250, nullable: false), Domain = table.Column<string>("nvarchar(253)", maxLength: 253, nullable: false), ContactName = table.Column<string>("nvarchar(max)", nullable: false),
            Email = table.Column<string>("nvarchar(320)", maxLength: 320, nullable: false), JobTitle = table.Column<string>("nvarchar(160)", maxLength: 160, nullable: false), Industry = table.Column<string>("nvarchar(120)", maxLength: 120, nullable: false),
            Country = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), Source = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false), FitScore = table.Column<int>("int", nullable: false), IntentScore = table.Column<int>("int", nullable: false),
            Status = table.Column<int>("int", nullable: false), LastEvaluatedAtUtc = table.Column<DateTime>("datetime2", nullable: true), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Prospects", x => x.Id));
        migrationBuilder.CreateIndex("IX_Prospects_TenantId_Domain", "Prospects", new[] { "TenantId", "Domain" }, unique: true, filter: "[Domain] <> N''");
        migrationBuilder.CreateIndex("IX_Prospects_TenantId_Email", "Prospects", new[] { "TenantId", "Email" }, unique: true, filter: "[Email] <> N''");
        migrationBuilder.CreateIndex("IX_Prospects_TenantId_Status_FitScore_IntentScore", "Prospects", new[] { "TenantId", "Status", "FitScore", "IntentScore" });

        migrationBuilder.CreateTable("ProspectSignals", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), ProspectId = table.Column<Guid>("uniqueidentifier", nullable: false), Type = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            Source = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), Evidence = table.Column<string>("nvarchar(max)", nullable: false), Score = table.Column<int>("int", nullable: false), SourceUrl = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: false),
            ObservedAtUtc = table.Column<DateTime>("datetime2", nullable: false), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ProspectSignals", x => x.Id));
        migrationBuilder.CreateIndex("IX_ProspectSignals_TenantId_ProspectId_ObservedAtUtc", "ProspectSignals", new[] { "TenantId", "ProspectId", "ObservedAtUtc" });

        migrationBuilder.CreateTable("TargetLists", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), Name = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false), IcpProfileId = table.Column<Guid>("uniqueidentifier", nullable: true),
            Description = table.Column<string>("nvarchar(max)", nullable: false), Dynamic = table.Column<bool>("bit", nullable: false), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_TargetLists", x => x.Id));
        migrationBuilder.CreateIndex("IX_TargetLists_TenantId_Name", "TargetLists", new[] { "TenantId", "Name" });

        migrationBuilder.CreateTable("TargetListMembers", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), TargetListId = table.Column<Guid>("uniqueidentifier", nullable: false), ProspectId = table.Column<Guid>("uniqueidentifier", nullable: false),
            AddedAtUtc = table.Column<DateTime>("datetime2", nullable: false), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_TargetListMembers", x => x.Id));
        migrationBuilder.CreateIndex("IX_TargetListMembers_TenantId_TargetListId_ProspectId", "TargetListMembers", new[] { "TenantId", "TargetListId", "ProspectId" }, unique: true);

        migrationBuilder.CreateTable("OutreachMessages", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), CampaignId = table.Column<Guid>("uniqueidentifier", nullable: false), ProspectId = table.Column<Guid>("uniqueidentifier", nullable: false), CampaignStepId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Channel = table.Column<string>("nvarchar(max)", nullable: false), Direction = table.Column<string>("nvarchar(max)", nullable: false), Subject = table.Column<string>("nvarchar(max)", nullable: false), Body = table.Column<string>("nvarchar(max)", nullable: false), Status = table.Column<int>("int", nullable: false), ProviderMessageId = table.Column<string>("nvarchar(max)", nullable: false),
            SentAtUtc = table.Column<DateTime>("datetime2", nullable: true), CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_OutreachMessages", x => x.Id));
        migrationBuilder.CreateIndex("IX_OutreachMessages_TenantId_CampaignId_ProspectId", "OutreachMessages", new[] { "TenantId", "CampaignId", "ProspectId" });

        migrationBuilder.CreateTable("ProspectReplies", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false), CampaignId = table.Column<Guid>("uniqueidentifier", nullable: false), ProspectId = table.Column<Guid>("uniqueidentifier", nullable: false), OutreachMessageId = table.Column<Guid>("uniqueidentifier", nullable: true),
            Body = table.Column<string>("nvarchar(max)", nullable: false), Classification = table.Column<string>("nvarchar(max)", nullable: false), SentimentScore = table.Column<int>("int", nullable: false), RequiresHuman = table.Column<bool>("bit", nullable: false), ReceivedAtUtc = table.Column<DateTime>("datetime2", nullable: false),
            CreatedAtUtc = table.Column<DateTime>("datetime2", nullable: false), UpdatedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ProspectReplies", x => x.Id));
        migrationBuilder.CreateIndex("IX_ProspectReplies_TenantId_CampaignId_ReceivedAtUtc", "ProspectReplies", new[] { "TenantId", "CampaignId", "ReceivedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "CampaignRecipients", "CampaignSteps", "OutreachMessages", "ProspectReplies", "ProspectSignals", "TargetListMembers", "Campaigns", "Prospects", "TargetLists" })
            migrationBuilder.DropTable(table);
        migrationBuilder.DropIndex("IX_IcpProfiles_TenantId_Active", "IcpProfiles");
        foreach (var column in new[] { "Active", "CountriesCsv", "Industry", "IntentKeywordsCsv", "LastDiscoveryAtUtc", "MaximumEmployees", "MinimumEmployees" })
            migrationBuilder.DropColumn(column, "IcpProfiles");
        migrationBuilder.AlterColumn<string>("Name", "IcpProfiles", "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(200)", oldMaxLength: 200);
    }
}
