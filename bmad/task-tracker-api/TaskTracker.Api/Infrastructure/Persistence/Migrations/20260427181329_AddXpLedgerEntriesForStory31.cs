using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddXpLedgerEntriesForStory31 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "XpLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskCompletionEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    XpGranted = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpLedgerEntries_TaskCompletionEvents_TaskCompletionEventId",
                        column: x => x.TaskCompletionEventId,
                        principalTable: "TaskCompletionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_XpLedgerEntries_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_XpLedgerEntries_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_XpLedgerEntries_OwnerId_OccurredAtUtc",
                table: "XpLedgerEntries",
                columns: new[] { "OwnerId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_XpLedgerEntries_OwnerId_TaskId_IdempotencyKey",
                table: "XpLedgerEntries",
                columns: new[] { "OwnerId", "TaskId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpLedgerEntries_TaskCompletionEventId",
                table: "XpLedgerEntries",
                column: "TaskCompletionEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpLedgerEntries_TaskId",
                table: "XpLedgerEntries",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XpLedgerEntries");
        }
    }
}
