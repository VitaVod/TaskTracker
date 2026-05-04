using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskTracker.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationFailureEventsStory74 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationProcessingFailureEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntegrationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalTaskId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorClass = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HttpStatus = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationProcessingFailureEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationProcessingFailureEvents_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProcessingFailureEvents_CorrelationId",
                table: "IntegrationProcessingFailureEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProcessingFailureEvents_IntegrationId_OccurredAtUtc",
                table: "IntegrationProcessingFailureEvents",
                columns: new[] { "IntegrationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProcessingFailureEvents_OccurredAtUtc",
                table: "IntegrationProcessingFailureEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProcessingFailureEvents_OwnerUserId_OccurredAtUtc",
                table: "IntegrationProcessingFailureEvents",
                columns: new[] { "OwnerUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProcessingFailureEvents_TraceId",
                table: "IntegrationProcessingFailureEvents",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationProcessingFailureEvents");
        }
    }
}
