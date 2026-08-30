using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830155000_AddRectificativeBillingRecords")]
public partial class AddRectificativeBillingRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RectifiesBillingRecordId",
            table: "BillingRecords",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RectificationType",
            table: "BillingRecords",
            type: "character varying(1)",
            maxLength: 1,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RectifiedBaseAmount",
            table: "BillingRecords",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RectifiedTaxAmount",
            table: "BillingRecords",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RectifiedSurchargeAmount",
            table: "BillingRecords",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BillingRecords_RectifiesBillingRecordId",
            table: "BillingRecords",
            column: "RectifiesBillingRecordId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_BillingRecords_RectifiesBillingRecordId",
            table: "BillingRecords");

        migrationBuilder.DropColumn(name: "RectifiesBillingRecordId", table: "BillingRecords");
        migrationBuilder.DropColumn(name: "RectificationType", table: "BillingRecords");
        migrationBuilder.DropColumn(name: "RectifiedBaseAmount", table: "BillingRecords");
        migrationBuilder.DropColumn(name: "RectifiedTaxAmount", table: "BillingRecords");
        migrationBuilder.DropColumn(name: "RectifiedSurchargeAmount", table: "BillingRecords");
    }
}
