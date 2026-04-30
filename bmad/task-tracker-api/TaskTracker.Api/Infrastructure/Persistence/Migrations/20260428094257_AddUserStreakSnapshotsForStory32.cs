using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStreakSnapshotsForStory32 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserStreakSnapshots",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentStreakDays = table.Column<int>(type: "int", nullable: false),
                    LongestStreakDays = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EvaluationWindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluationWindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEvaluatedEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastEvaluationTraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStreakSnapshots", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_UserStreakSnapshots_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStreakSnapshots");
        }
    }
}
