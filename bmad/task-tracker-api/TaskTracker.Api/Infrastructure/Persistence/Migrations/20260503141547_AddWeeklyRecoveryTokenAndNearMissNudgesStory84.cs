using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyRecoveryTokenAndNearMissNudgesStory84 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRecoveryTokenConsumedAtUtc",
                table: "UserStreakSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRecoveryTokenGrantedAtUtc",
                table: "UserStreakSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecoveryTokenBalance",
                table: "UserStreakSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryTokenWeekKey",
                table: "UserStreakSnapshots",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StreakRecoveryTokenEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LocalDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WeekKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreakRecoveryTokenEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreakRecoveryTokenEvents_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreakRecoveryTokenEvents_OwnerId_OccurredAtUtc",
                table: "StreakRecoveryTokenEvents",
                columns: new[] { "OwnerId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StreakRecoveryTokenEvents_OwnerId_WeekKey",
                table: "StreakRecoveryTokenEvents",
                columns: new[] { "OwnerId", "WeekKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreakRecoveryTokenEvents");

            migrationBuilder.DropColumn(
                name: "LastRecoveryTokenConsumedAtUtc",
                table: "UserStreakSnapshots");

            migrationBuilder.DropColumn(
                name: "LastRecoveryTokenGrantedAtUtc",
                table: "UserStreakSnapshots");

            migrationBuilder.DropColumn(
                name: "RecoveryTokenBalance",
                table: "UserStreakSnapshots");

            migrationBuilder.DropColumn(
                name: "RecoveryTokenWeekKey",
                table: "UserStreakSnapshots");
        }
    }
}
