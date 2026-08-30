using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830103000_InitialPostgreSql")]
public partial class InitialPostgreSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BillingRecords",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                IssuerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                RecipientNif = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                RecipientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RegisterTimestamp = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                PreviousBillingRecordId = table.Column<int>(type: "integer", nullable: true),
                PreviousRecordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ComputedHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IsSubmitted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                SubmissionCorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                AeatSubmissionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pendiente"),
                IssuerNif = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                InvoiceSeries = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                FiscalInvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TotalTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BillingRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "DeadLetterMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OriginalMessageId = table.Column<long>(type: "bigint", nullable: false),
                CorrelationId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                LastErrorResponse = table.Column<string>(type: "text", nullable: true),
                ProcessingAttempts = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                MovedToDlqAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsReviewed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_DeadLetterMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                AggregateId = table.Column<int>(type: "integer", nullable: false),
                AggregateType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ProcessingAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                LastProcessingError = table.Column<string>(type: "text", nullable: true),
                IsProcessed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LockedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SubmissionAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BillingRecordId = table.Column<int>(type: "integer", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                RequestPayload = table.Column<string>(type: "text", nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResponseCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ResponseDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ResponsePayload = table.Column<string>(type: "text", nullable: true),
                AeatSubmissionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Status = table.Column<string>(type: "text", nullable: false),
                RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DurationMilliseconds = table.Column<int>(type: "integer", nullable: true),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubmissionAttempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_SubmissionAttempts_BillingRecords_BillingRecordId",
                    column: x => x.BillingRecordId,
                    principalTable: "BillingRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_BillingRecords_Issuer_GenerationOrder", table: "BillingRecords", columns: new[] { "IssuerNif", "Id" });
        migrationBuilder.CreateIndex(name: "IX_BillingRecords_PreviousBillingRecordId", table: "BillingRecords", column: "PreviousBillingRecordId");
        migrationBuilder.CreateIndex(name: "UX_BillingRecords_FiscalIdentity", table: "BillingRecords", columns: new[] { "IssuerNif", "FiscalInvoiceNumber", "IssueDate" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_DeadLetterMessages_CorrelationId", table: "DeadLetterMessages", column: "CorrelationId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_DeadLetterMessages_UnreviewedByDate", table: "DeadLetterMessages", columns: new[] { "IsReviewed", "MovedToDlqAt" });
        migrationBuilder.CreateIndex(name: "IX_OutboxMessages_AggregateId", table: "OutboxMessages", column: "AggregateId");
        migrationBuilder.CreateIndex(name: "IX_OutboxMessages_Claim", table: "OutboxMessages", columns: new[] { "IsProcessed", "NextAttemptAt", "LockedUntil", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_OutboxMessages_CorrelationId", table: "OutboxMessages", column: "CorrelationId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_OutboxMessages_IsProcessed", table: "OutboxMessages", column: "IsProcessed");
        migrationBuilder.CreateIndex(name: "UX_OutboxMessages_AggregateEvent", table: "OutboxMessages", columns: new[] { "AggregateType", "AggregateId", "EventType" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_SubmissionAttempts_BillingRecordId", table: "SubmissionAttempts", column: "BillingRecordId");
        migrationBuilder.CreateIndex(name: "IX_SubmissionAttempts_StatusAndTime", table: "SubmissionAttempts", columns: new[] { "Status", "SubmittedAt" });
        migrationBuilder.CreateIndex(name: "UX_SubmissionAttempts_BillingRecordAndAttempt", table: "SubmissionAttempts", columns: new[] { "BillingRecordId", "AttemptNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DeadLetterMessages");
        migrationBuilder.DropTable(name: "OutboxMessages");
        migrationBuilder.DropTable(name: "SubmissionAttempts");
        migrationBuilder.DropTable(name: "BillingRecords");
    }
}
