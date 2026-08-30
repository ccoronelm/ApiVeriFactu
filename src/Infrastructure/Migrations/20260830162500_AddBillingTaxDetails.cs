using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830162500_AddBillingTaxDetails")]
public partial class AddBillingTaxDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BillingTaxDetails",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BillingRecordId = table.Column<int>(type: "integer", nullable: false),
                TaxCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                RegimeCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                OperationQualification = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                ExemptionCause = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                TaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                TaxBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                EquivalenceSurchargeRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                EquivalenceSurchargeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: true),
                LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifiedBy = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingTaxDetails", x => x.Id);
                table.ForeignKey(
                    name: "FK_BillingTaxDetails_BillingRecords_BillingRecordId",
                    column: x => x.BillingRecordId,
                    principalTable: "BillingRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BillingTaxDetails_Record_Order",
            table: "BillingTaxDetails",
            columns: new[] { "BillingRecordId", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BillingTaxDetails");
    }
}
