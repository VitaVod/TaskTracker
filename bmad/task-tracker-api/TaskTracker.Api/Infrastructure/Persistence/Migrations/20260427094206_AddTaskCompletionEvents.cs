using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCompletionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskCompletionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultingIsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCompletionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskCompletionEvents_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskCompletionEvents_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskCompletionEvents_OccurredAtUtc",
                table: "TaskCompletionEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TaskCompletionEvents_OwnerId",
                table: "TaskCompletionEvents",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskCompletionEvents_TaskId_OwnerId_IdempotencyKey",
                table: "TaskCompletionEvents",
                columns: new[] { "TaskId", "OwnerId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskCompletionEvents");
        }
    }
}
