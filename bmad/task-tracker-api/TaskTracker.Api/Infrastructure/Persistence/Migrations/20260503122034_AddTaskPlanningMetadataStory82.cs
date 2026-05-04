using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPlanningMetadataStory82 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextTag",
                table: "Tasks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Tasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "easy");

            migrationBuilder.AddColumn<int>(
                name: "EffortPoints",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnergyLevel",
                table: "Tasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "medium");

            migrationBuilder.AddColumn<int>(
                name: "PredictedDurationMinutes",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UserId_EnergyLevel_ContextTag",
                table: "Tasks",
                columns: new[] { "UserId", "EnergyLevel", "ContextTag" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_UserId_EnergyLevel_ContextTag",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ContextTag",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EffortPoints",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EnergyLevel",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PredictedDurationMinutes",
                table: "Tasks");
        }
    }
}
