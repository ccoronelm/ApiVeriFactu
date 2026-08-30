using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830131500_AddBillingRecordSubsanation")]
public partial class AddBillingRecordSubsanation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SubsanatesBillingRecordId",
            table: "BillingRecords",
            type: "integer",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords");

        migrationBuilder.CreateIndex(
            name: "IX_BillingRecords_SubsanatesBillingRecordId",
            table: "BillingRecords",
            column: "SubsanatesBillingRecordId");

        migrationBuilder.CreateIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords",
            columns: new[] { "IssuerNif", "FiscalInvoiceNumber", "IssueDate" },
            unique: true,
            filter: "\"SubsanatesBillingRecordId\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_BillingRecords_SubsanatesBillingRecordId",
            table: "BillingRecords");

        migrationBuilder.DropIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "SubsanatesBillingRecordId",
            table: "BillingRecords");

        migrationBuilder.CreateIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords",
            columns: new[] { "IssuerNif", "FiscalInvoiceNumber", "IssueDate" },
            unique: true);
    }
}
