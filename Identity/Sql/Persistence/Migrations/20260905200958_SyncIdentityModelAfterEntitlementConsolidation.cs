using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Identity.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SyncIdentityModelAfterEntitlementConsolidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GracePeriodEndsAtUtc",
                table: "Licenses",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GracePeriodEndsAtUtc",
                table: "Licenses");
        }
    }
}
