using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationTaskSyncBindingsStory72 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationTaskSyncBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalTaskId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationTaskSyncBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationTaskSyncBindings_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegrationTaskSyncBindings_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
						onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTaskSyncBindings_OwnerUserId_IntegrationId_ExternalTaskId",
                table: "IntegrationTaskSyncBindings",
                columns: new[] { "OwnerUserId", "IntegrationId", "ExternalTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTaskSyncBindings_TaskId",
                table: "IntegrationTaskSyncBindings",
                column: "TaskId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationTaskSyncBindings");
        }
    }
}
