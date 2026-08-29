using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubmissionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingRecordId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResponseDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AeatSubmissionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMilliseconds = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_BillingRecordAndAttempt",
                table: "SubmissionAttempts",
                columns: new[] { "BillingRecordId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_BillingRecordId",
                table: "SubmissionAttempts",
                column: "BillingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_StatusAndTime",
                table: "SubmissionAttempts",
                columns: new[] { "Status", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionAttempts");
        }
    }
}
