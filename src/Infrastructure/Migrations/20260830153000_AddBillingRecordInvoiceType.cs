using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830153000_AddBillingRecordInvoiceType")]
public partial class AddBillingRecordInvoiceType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InvoiceType",
            table: "BillingRecords",
            type: "character varying(2)",
            maxLength: 2,
            nullable: false,
            defaultValue: "F1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InvoiceType",
            table: "BillingRecords");
    }
}
