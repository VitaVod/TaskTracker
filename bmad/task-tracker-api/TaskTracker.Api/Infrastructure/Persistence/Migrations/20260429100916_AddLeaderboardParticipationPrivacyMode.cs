using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardParticipationPrivacyMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskCompletionEvents_OwnerId",
                table: "TaskCompletionEvents");

            migrationBuilder.AddColumn<string>(
                name: "LeaderboardParticipationMode",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "hidden");

            migrationBuilder.CreateIndex(
                name: "IX_UserStreakSnapshots_CurrentStreakDays_OwnerId",
                table: "UserStreakSnapshots",
                columns: new[] { "CurrentStreakDays", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskCompletionEvents_OwnerId_EventName_OccurredAtUtc",
                table: "TaskCompletionEvents",
                columns: new[] { "OwnerId", "EventName", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserStreakSnapshots_CurrentStreakDays_OwnerId",
                table: "UserStreakSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_TaskCompletionEvents_OwnerId_EventName_OccurredAtUtc",
                table: "TaskCompletionEvents");

            migrationBuilder.DropColumn(
                name: "LeaderboardParticipationMode",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_TaskCompletionEvents_OwnerId",
                table: "TaskCompletionEvents",
                column: "OwnerId");
        }
    }
}
