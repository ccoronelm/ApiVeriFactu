using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gesFactu.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830070000_HardenOutboxLeasing")]
public partial class HardenOutboxLeasing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LockedBy",
            table: "OutboxMessages",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockedUntil",
            table: "OutboxMessages",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextAttemptAt",
            table: "OutboxMessages",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Claim",
            table: "OutboxMessages",
            columns: new[] { "IsProcessed", "NextAttemptAt", "LockedUntil", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "UX_OutboxMessages_AggregateEvent",
            table: "OutboxMessages",
            columns: new[] { "AggregateType", "AggregateId", "EventType" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OutboxMessages_Claim",
            table: "OutboxMessages");

        migrationBuilder.DropIndex(
            name: "UX_OutboxMessages_AggregateEvent",
            table: "OutboxMessages");

        migrationBuilder.DropColumn(
            name: "LockedBy",
            table: "OutboxMessages");

        migrationBuilder.DropColumn(
            name: "LockedUntil",
            table: "OutboxMessages");

        migrationBuilder.DropColumn(
            name: "NextAttemptAt",
            table: "OutboxMessages");
    }
}
