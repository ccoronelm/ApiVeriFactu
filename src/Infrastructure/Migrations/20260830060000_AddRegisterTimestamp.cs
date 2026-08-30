using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using gesFactu.Infrastructure.Persistence;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830060000_AddRegisterTimestamp")]
public partial class AddRegisterTimestamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RegisterTimestamp",
            table: "BillingRecords",
            type: "nvarchar(25)",
            maxLength: 25,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RegisterTimestamp",
            table: "BillingRecords");
    }
}
