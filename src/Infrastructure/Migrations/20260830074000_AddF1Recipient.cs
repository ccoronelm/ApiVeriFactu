using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830074000_AddF1Recipient")]
public partial class AddF1Recipient : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecipientNif",
            table: "BillingRecords",
            type: "nvarchar(9)",
            maxLength: 9,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "RecipientName",
            table: "BillingRecords",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RecipientNif",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "RecipientName",
            table: "BillingRecords");
    }
}
