using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830071000_HardenSubmissionAttempts")]
public partial class HardenSubmissionAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SubmissionAttempts_BillingRecordAndAttempt",
            table: "SubmissionAttempts");

        migrationBuilder.CreateIndex(
            name: "UX_SubmissionAttempts_BillingRecordAndAttempt",
            table: "SubmissionAttempts",
            columns: new[] { "BillingRecordId", "AttemptNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_SubmissionAttempts_BillingRecordAndAttempt",
            table: "SubmissionAttempts");

        migrationBuilder.CreateIndex(
            name: "IX_SubmissionAttempts_BillingRecordAndAttempt",
            table: "SubmissionAttempts",
            columns: new[] { "BillingRecordId", "AttemptNumber" });
    }
}
