using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830150000_AddBillingRecordCancellation")]
public partial class AddBillingRecordCancellation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecordType",
            table: "BillingRecords",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "Alta");

        migrationBuilder.AddColumn<int>(
            name: "CancelsBillingRecordId",
            table: "BillingRecords",
            type: "integer",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords");

        migrationBuilder.CreateIndex(
            name: "IX_BillingRecords_CancelsBillingRecordId",
            table: "BillingRecords",
            column: "CancelsBillingRecordId");

        migrationBuilder.CreateIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords",
            columns: new[] { "IssuerNif", "FiscalInvoiceNumber", "IssueDate" },
            unique: true,
            filter: "\"RecordType\" = 'Alta' AND \"SubsanatesBillingRecordId\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_BillingRecords_CancelsBillingRecordId",
            table: "BillingRecords");

        migrationBuilder.DropIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "CancelsBillingRecordId",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "RecordType",
            table: "BillingRecords");

        migrationBuilder.CreateIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords",
            columns: new[] { "IssuerNif", "FiscalInvoiceNumber", "IssueDate" },
            unique: true,
            filter: "\"SubsanatesBillingRecordId\" IS NULL");
    }
}
