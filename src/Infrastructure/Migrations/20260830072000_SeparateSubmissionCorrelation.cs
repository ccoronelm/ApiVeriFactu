using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830072000_SeparateSubmissionCorrelation")]
public partial class SeparateSubmissionCorrelation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SubmissionCorrelationId",
            table: "BillingRecords",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "AeatSubmissionId",
            table: "BillingRecords",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SubmissionCorrelationId",
            table: "BillingRecords");

        migrationBuilder.AlterColumn<string>(
            name: "AeatSubmissionId",
            table: "BillingRecords",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);
    }
}
