using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830230000_AddProductionSecurityIntegrity")]
public partial class AddProductionSecurityIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BillingTaxDetails_BillingRecords_BillingRecordId",
            table: "BillingTaxDetails");

        migrationBuilder.DropForeignKey(
            name: "FK_SubmissionAttempts_BillingRecords_BillingRecordId",
            table: "SubmissionAttempts");

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "BillingRecords",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "BillingRecords",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeletedBy",
            table: "BillingRecords",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "BillingTaxDetails",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "BillingTaxDetails",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeletedBy",
            table: "BillingTaxDetails",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreateDate",
            table: "BillingTaxDetails",
            type: "timestamp with time zone",
            nullable: true,
            defaultValueSql: "CURRENT_TIMESTAMP",
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "CreatedBy",
            table: "BillingTaxDetails",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LastModifiedBy",
            table: "BillingTaxDetails",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                EntityName = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false),
                EntityId = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false),
                Action = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                Actor = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false),
                CorrelationId = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),
                OccurredAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                OldValues = table.Column<string>(
                    type: "jsonb",
                    nullable: true),
                NewValues = table.Column<string>(
                    type: "jsonb",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "IdempotencyRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                Key = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false),
                Method = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                Path = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false),
                RequestHash = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                Status = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                ResponseStatusCode = table.Column<int>(
                    type: "integer",
                    nullable: true),
                ResponseContentType = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true),
                ResponseBody = table.Column<string>(
                    type: "text",
                    nullable: true),
                ResponseLocation = table.Column<string>(
                    type: "character varying(1000)",
                    maxLength: 1000,
                    nullable: true),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                CompletedAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true),
                ExpiresAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CorrelationId",
            table: "AuditLogs",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Entity_Time",
            table: "AuditLogs",
            columns: new[]
            {
                "EntityName",
                "EntityId",
                "OccurredAtUtc"
            });

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyRecords_ExpiresAt",
            table: "IdempotencyRecords",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "UX_IdempotencyRecords_Key_Method_Path",
            table: "IdempotencyRecords",
            columns: new[] { "Key", "Method", "Path" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_BillingTaxDetails_BillingRecords_BillingRecordId",
            table: "BillingTaxDetails",
            column: "BillingRecordId",
            principalTable: "BillingRecords",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SubmissionAttempts_BillingRecords_BillingRecordId",
            table: "SubmissionAttempts",
            column: "BillingRecordId",
            principalTable: "BillingRecords",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BillingTaxDetails_BillingRecords_BillingRecordId",
            table: "BillingTaxDetails");

        migrationBuilder.DropForeignKey(
            name: "FK_SubmissionAttempts_BillingRecords_BillingRecordId",
            table: "SubmissionAttempts");

        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropTable(name: "IdempotencyRecords");

        migrationBuilder.DropColumn(
            name: "IsDeleted",
            table: "BillingRecords");
        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "BillingRecords");
        migrationBuilder.DropColumn(
            name: "DeletedBy",
            table: "BillingRecords");

        migrationBuilder.DropColumn(
            name: "IsDeleted",
            table: "BillingTaxDetails");
        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "BillingTaxDetails");
        migrationBuilder.DropColumn(
            name: "DeletedBy",
            table: "BillingTaxDetails");

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreateDate",
            table: "BillingTaxDetails",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone",
            oldNullable: true,
            oldDefaultValueSql: "CURRENT_TIMESTAMP");

        migrationBuilder.AlterColumn<string>(
            name: "CreatedBy",
            table: "BillingTaxDetails",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(256)",
            oldMaxLength: 256,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LastModifiedBy",
            table: "BillingTaxDetails",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(256)",
            oldMaxLength: 256,
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_BillingTaxDetails_BillingRecords_BillingRecordId",
            table: "BillingTaxDetails",
            column: "BillingRecordId",
            principalTable: "BillingRecords",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SubmissionAttempts_BillingRecords_BillingRecordId",
            table: "SubmissionAttempts",
            column: "BillingRecordId",
            principalTable: "BillingRecords",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
