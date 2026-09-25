using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260926011700_SeedIndustryPacks")]
public partial class SeedIndustryPacks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM [IndustryPacks] WHERE [Code] = N'fusionfleet-mk')
BEGIN
    INSERT INTO [IndustryPacks]
        ([Id], [Code], [Name], [Description], [TemplateJson], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES
        ('8d6f3f1e-7d41-4f31-9c2a-6e4b7a1f52d9',
         N'fusionfleet-mk',
         N'FusionFleet Mk',
         N'Customer acquisition for logistics, freight, transportation and 3PL businesses in North Macedonia and the wider Balkans.',
         N'{
  "industry": "Logistics & Transportation",
  "region": "North Macedonia / Balkans",
  "countries": ["North Macedonia", "Kosovo", "Albania", "Serbia", "Montenegro", "Bulgaria", "Greece"],
  "keywords": [
    "freight forwarding",
    "logistics",
    "transportation",
    "3PL",
    "road freight",
    "cargo",
    "warehousing",
    "distribution"
  ],
  "minimumScore": 70,
  "campaignName": "FusionFleet Mk Acquisition",
  "objective": "Discover, qualify and engage high-fit logistics and transportation companies that may need freight, fleet, warehousing or 3PL services.",
  "goal": "book-demo",
  "senderName": "FusionFleet Growth",
  "senderEmail": "",
  "steps": [
    {
      "stepNumber": 1,
      "delayHours": 0,
      "channel": "email",
      "subjectTemplate": "{{company}}: logistics capacity and efficiency",
      "bodyTemplate": "Hi {{contact}},\n\nI noticed {{company}} operates in logistics or transportation. FusionFleet helps teams improve freight, fleet and operational workflows.\n\nWould a short introduction be useful?"
    },
    {
      "stepNumber": 2,
      "delayHours": 48,
      "channel": "email",
      "subjectTemplate": "Re: {{company}} logistics operations",
      "bodyTemplate": "Hi {{contact}},\n\nFollowing up on my note about logistics operations. If improving capacity, visibility or operational efficiency is currently a priority, I can share a concise overview.\n\nWorth a look?"
    },
    {
      "stepNumber": 3,
      "delayHours": 120,
      "channel": "email",
      "subjectTemplate": "Close the loop — {{company}}",
      "bodyTemplate": "Hi {{contact}},\n\nI will close the loop here. If logistics growth or operational efficiency becomes a priority, I would be happy to reconnect.\n\nBest,\n{{sender}}"
    }
  ]
}',
         SYSUTCDATETIME(),
         SYSUTCDATETIME());
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DELETE FROM [IndustryPacks]
WHERE [Code] = N'fusionfleet-mk';
""");
    }
}
