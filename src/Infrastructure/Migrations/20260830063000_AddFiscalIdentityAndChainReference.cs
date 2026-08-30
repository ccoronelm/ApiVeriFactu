using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using gesFactu.Infrastructure.Persistence;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830063000_AddFiscalIdentityAndChainReference")]
public partial class AddFiscalIdentityAndChainReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FiscalInvoiceNumber",
            table: "BillingRecords",
            type: "nvarchar(60)",
            maxLength: 60,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "PreviousBillingRecordId",
            table: "BillingRecords",
            type: "int",
            nullable: true);

        // Backfill con la misma regla usada para NumSerieFactura en gesFactu:
        // serie + número, sin introducir separadores.
        migrationBuilder.Sql(
            """
            UPDATE BillingRecords
            SET FiscalInvoiceNumber =
                LTRIM(RTRIM(InvoiceSeries)) + LTRIM(RTRIM(InvoiceNumber))
            WHERE FiscalInvoiceNumber = '';
            """);

        migrationBuilder.CreateIndex(
            name: "IX_BillingRecords_Issuer_GenerationOrder",
            table: "BillingRecords",
            columns: new[] { "IssuerNif", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_BillingRecords_PreviousBillingRecordId",
            table: "BillingRecords",
            column: "PreviousBillingRecordId");

        migrationBuilder.CreateIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords",
            columns: new[] { "IssuerNif", "FiscalInvoiceNumber", "IssueDate" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_BillingRecords_Issuer_GenerationOrder",
            table: "BillingRecords");

        migrationBuilder.DropIndex(
            name: "IX_BillingRecords_PreviousBillingRecordId",
            table: "BillingRecords");

        migrationBuilder.DropIndex(
            name: "UX_BillingRecords_FiscalIdentity",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "FiscalInvoiceNumber",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "PreviousBillingRecordId",
            table: "BillingRecords");
    }
}
